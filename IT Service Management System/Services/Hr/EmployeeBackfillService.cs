using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Populates the employee register from data that already exists, so introducing it does not
    /// start everyone from an empty table.
    /// <para>
    /// Two passes. First, an employee record is created for every user account that has none.
    /// Second, the historical interview and talent rows — which recorded the person only as typed
    /// text — are matched back to an employee by name. Both passes are idempotent and conservative:
    /// a name that matches more than one employee is left unlinked for a human to resolve rather
    /// than guessed at, because attaching an exit interview to the wrong person is worse than
    /// leaving it detached.
    /// </para>
    /// </summary>
    public class EmployeeBackfillService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<EmployeeBackfillService> _log;

        public EmployeeBackfillService(ApplicationDbContext db, ILogger<EmployeeBackfillService> log)
        {
            _db = db; _log = log;
        }

        /// <summary>Result of a backfill run, for logging and for the admin-triggered re-run.</summary>
        public record BackfillResult(int EmployeesCreated, int TalentLinked, int ExitLinked, int StayLinked, int Ambiguous)
        {
            public int TotalLinked => TalentLinked + ExitLinked + StayLinked;
        }

        public async Task<BackfillResult> RunAsync(CancellationToken ct = default)
        {
            var created = await CreateFromUserAccountsAsync(ct);
            var (talent, exit, stay, ambiguous) = await LinkHistoricalRecordsAsync(ct);

            var result = new BackfillResult(created, talent, exit, stay, ambiguous);
            if (created > 0 || result.TotalLinked > 0 || ambiguous > 0)
                _log.LogInformation(
                    "Employee backfill: {Created} record(s) created, {Linked} historical row(s) linked, {Ambiguous} left for manual matching.",
                    created, result.TotalLinked, ambiguous);

            return result;
        }

        // ── Pass 1: an employee record per user account ──────────────────────────

        private async Task<int> CreateFromUserAccountsAsync(CancellationToken ct)
        {
            var linkedUserIds = await _db.Employees.IgnoreQueryFilters()
                .Where(e => e.UserId != null)
                .Select(e => e.UserId!.Value)
                .ToListAsync(ct);

            var unlinked = await _db.Users.AsNoTracking()
                .Where(u => !linkedUserIds.Contains(u.Id))
                .OrderBy(u => u.Id)
                .ToListAsync(ct);

            if (unlinked.Count == 0) return 0;

            // Continue the existing numbering rather than restarting it.
            var nextNumber = await NextEmployeeNumberSeedAsync(ct);

            foreach (var user in unlinked)
            {
                _db.Employees.Add(new Employee
                {
                    EmployeeNumber = $"EMP-{nextNumber++:D5}",
                    UserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    WorkEmail = user.Email,
                    MobileNumber = user.Phone,
                    DepartmentId = user.DepartmentId,
                    // The account's role is the closest thing to a job title we have; HR can correct it.
                    JobTitle = SplitCamelCase(user.Role.ToString()),
                    // CreatedAt on the account is the best available proxy for a start date.
                    HireDate = user.CreatedAt.Date,
                    Status = user.IsActive ? EmploymentStatus.Active : EmploymentStatus.Resigned,
                    Notes = "Created automatically from the user account when the employee register "
                          + "was introduced. Job title and hire date are estimates — please confirm.",
                    CreatedAt = DateTime.Now
                });
            }

            await _db.SaveChangesAsync(ct);

            // The manager hierarchy needs every employee to exist first, so it is a second pass.
            await LinkManagersFromSupervisorsAsync(ct);

            return unlinked.Count;
        }

        /// <summary>Mirror the account supervisor chain onto the employee reporting line.</summary>
        private async Task LinkManagersFromSupervisorsAsync(CancellationToken ct)
        {
            var employeesByUserId = await _db.Employees
                .Where(e => e.UserId != null)
                .ToDictionaryAsync(e => e.UserId!.Value, e => e, ct);

            var supervisors = await _db.Users.AsNoTracking()
                .Where(u => u.SupervisorId != null)
                .Select(u => new { u.Id, SupervisorId = u.SupervisorId!.Value })
                .ToListAsync(ct);

            var changed = false;
            foreach (var link in supervisors)
            {
                if (!employeesByUserId.TryGetValue(link.Id, out var employee)) continue;
                if (employee.ManagerId != null) continue;
                if (!employeesByUserId.TryGetValue(link.SupervisorId, out var manager)) continue;
                if (manager.Id == employee.Id) continue;   // never let somebody manage themselves

                employee.ManagerId = manager.Id;
                changed = true;
            }

            if (changed) await _db.SaveChangesAsync(ct);
        }

        private async Task<int> NextEmployeeNumberSeedAsync(CancellationToken ct)
        {
            var existing = await _db.Employees.IgnoreQueryFilters()
                .Where(e => e.EmployeeNumber.StartsWith("EMP-"))
                .Select(e => e.EmployeeNumber)
                .ToListAsync(ct);

            return existing
                .Select(n => int.TryParse(n[4..], out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
        }

        // ── Pass 2: match historical free-text rows to an employee ───────────────

        private async Task<(int Talent, int Exit, int Stay, int Ambiguous)> LinkHistoricalRecordsAsync(CancellationToken ct)
        {
            var employees = await _db.Employees.AsNoTracking()
                .Select(e => new { e.Id, e.FirstName, e.LastName, e.PreferredName })
                .ToListAsync(ct);
            if (employees.Count == 0) return (0, 0, 0, 0);

            // Group by normalised name so a duplicate is detectable rather than silently picked.
            var byName = employees
                .SelectMany(e => NameKeysFor(e.FirstName, e.LastName, e.PreferredName)
                    .Select(key => new { Key = key, e.Id }))
                .GroupBy(x => x.Key)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).Distinct().ToList());

            var ambiguous = 0;

            int? Resolve(string? typedName)
            {
                var key = Normalise(typedName);
                if (key.Length == 0 || !byName.TryGetValue(key, out var matches)) return null;
                if (matches.Count > 1) { ambiguous++; return null; }   // two people share the name
                return matches[0];
            }

            var talentLinked = 0;
            foreach (var row in await _db.TalentIdentifications.Where(t => t.EmployeeId == null).ToListAsync(ct))
                if (Resolve(row.EmployeeName) is int id) { row.EmployeeId = id; talentLinked++; }

            var exitLinked = 0;
            foreach (var row in await _db.ExitInterviews.Where(t => t.EmployeeId == null).ToListAsync(ct))
                if (Resolve(row.EmployeeName) is int id) { row.EmployeeId = id; exitLinked++; }

            var stayLinked = 0;
            foreach (var row in await _db.EngagementStayInterviews.Where(t => t.EmployeeId == null).ToListAsync(ct))
                if (Resolve(row.NameAndSurname) is int id) { row.EmployeeId = id; stayLinked++; }

            if (talentLinked + exitLinked + stayLinked > 0) await _db.SaveChangesAsync(ct);
            return (talentLinked, exitLinked, stayLinked, ambiguous);
        }

        /// <summary>
        /// The forms of a name a historical row might have been typed as: "First Last",
        /// "Last First", and the preferred-name variants.
        /// </summary>
        private static IEnumerable<string> NameKeysFor(string first, string last, string? preferred)
        {
            yield return Normalise($"{first} {last}");
            yield return Normalise($"{last} {first}");
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                yield return Normalise($"{preferred} {last}");
                yield return Normalise($"{last} {preferred}");
            }
        }

        /// <summary>Lower-case, strip punctuation and collapse whitespace so "O'Brien" matches "OBrien".</summary>
        private static string Normalise(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var chars = value.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray();
            return string.Join(' ', new string(chars).ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static string SplitCamelCase(string value) =>
            System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");
    }
}
