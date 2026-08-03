using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Hr;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Hr;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// The employee register — HR's own surface for onboarding people, maintaining their
    /// employment details and reporting line, and taking them off the payroll.
    /// <para>
    /// Deliberately separate from <c>UsersController</c>, which stays administrator-only. HR owns
    /// employment data; administrators own credentials and roles. That split means HR can do its
    /// job without being able to grant itself a role, which is the reason HR was locked out of
    /// user administration in the first place.
    /// </para>
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly EmployeeBackfillService _backfill;

        public EmployeesController(ApplicationDbContext db, AuditService audit, EmployeeBackfillService backfill)
        {
            _db = db; _audit = audit; _backfill = backfill;
        }

        private const int PageSize = 25;

        // ════════════════════════════════════════════════════════════════════════
        //  Register
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index(string? q, int? departmentId, EmploymentStatus? status,
            EmploymentType? type, bool currentOnly = true, int page = 1)
        {
            IQueryable<Employee> query = _db.Employees.AsNoTracking()
                .Include(e => e.Department).Include(e => e.Manager).Include(e => e.User);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(e =>
                    e.FirstName.Contains(term) || e.LastName.Contains(term)
                    || e.EmployeeNumber.Contains(term) || e.JobTitle.Contains(term)
                    || (e.PreferredName != null && e.PreferredName.Contains(term))
                    || (e.WorkEmail != null && e.WorkEmail.Contains(term)));
            }

            if (departmentId.HasValue) query = query.Where(e => e.DepartmentId == departmentId.Value);
            if (status.HasValue) query = query.Where(e => e.Status == status.Value);
            if (type.HasValue) query = query.Where(e => e.EmploymentType == type.Value);

            // "Current" means still on the payroll in any capacity — the default view, because
            // leavers otherwise swamp the list within a couple of years.
            if (currentOnly)
                query = query.Where(e => e.Status == EmploymentStatus.Active
                    || e.Status == EmploymentStatus.OnProbation
                    || e.Status == EmploymentStatus.OnLeave
                    || e.Status == EmploymentStatus.Suspended);

            var (items, paging) = await query
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .PageAsync(page, PageSize);

            ViewBag.Paging = paging;
            ViewBag.Q = q; ViewBag.DepartmentId = departmentId; ViewBag.Status = status;
            ViewBag.Type = type; ViewBag.CurrentOnly = currentOnly;

            // Headline counts, computed over the whole register rather than the current page.
            var all = await _db.Employees.AsNoTracking()
                .Select(e => new { e.Status, e.ProbationEndDate, e.ContractEndDate })
                .ToListAsync();
            ViewBag.Headcount = all.Count(e => e.Status is EmploymentStatus.Active
                or EmploymentStatus.OnProbation or EmploymentStatus.OnLeave or EmploymentStatus.Suspended);
            ViewBag.OnProbation = all.Count(e => e.Status == EmploymentStatus.OnProbation);
            ViewBag.ProbationDue = all.Count(e => e.Status == EmploymentStatus.OnProbation
                && e.ProbationEndDate != null && e.ProbationEndDate <= DateTime.Today.AddDays(30));
            ViewBag.ContractsExpiring = all.Count(e =>
                (e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.OnProbation)
                && e.ContractEndDate != null && e.ContractEndDate <= DateTime.Today.AddDays(60));
            ViewBag.Leavers = all.Count(e => e.Status is EmploymentStatus.Resigned
                or EmploymentStatus.Terminated or EmploymentStatus.Retired or EmploymentStatus.EndOfContract);

            await PopulateListsAsync();
            return View(items);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Create / edit
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Create()
        {
            await PopulateListsAsync();
            return View("Form", new Employee
            {
                EmployeeNumber = await NextEmployeeNumberAsync(),
                HireDate = DateTime.Today,
                Status = EmploymentStatus.OnProbation,
                ProbationEndDate = DateTime.Today.AddMonths(3)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create(Employee input)
        {
            await ValidateAsync(input);
            if (!ModelState.IsValid) { await PopulateListsAsync(); return View("Form", input); }

            input.EmployeeNumber = string.IsNullOrWhiteSpace(input.EmployeeNumber)
                ? await NextEmployeeNumberAsync()
                : input.EmployeeNumber.Trim();
            input.CreatedAt = DateTime.Now;

            _db.Employees.Add(input);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", nameof(Employee), input.Id,
                $"{input.EmployeeNumber} — {input.FullName}, {input.JobTitle}");

            TempData["Success"] = $"{input.FullName} added to the employee register as {input.EmployeeNumber}.";
            return RedirectToAction(nameof(Details), new { id = input.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            await PopulateListsAsync(id);
            return View("Form", employee);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Employee input)
        {
            var employee = await _db.Employees.FindAsync(input.Id);
            if (employee == null) return NotFound();

            await ValidateAsync(input);
            if (!ModelState.IsValid) { await PopulateListsAsync(input.Id); return View("Form", input); }

            // Note what changed before overwriting, so the audit entry is specific rather than
            // just "record updated".
            var changes = DescribeChanges(employee, input);

            employee.EmployeeNumber = input.EmployeeNumber.Trim();
            employee.UserId = input.UserId;
            employee.FirstName = input.FirstName;
            employee.LastName = input.LastName;
            employee.MiddleName = input.MiddleName;
            employee.PreferredName = input.PreferredName;
            employee.Gender = input.Gender;
            employee.DateOfBirth = input.DateOfBirth;
            employee.NationalId = input.NationalId;
            employee.Nationality = input.Nationality;
            employee.MaritalStatus = input.MaritalStatus;
            employee.WorkEmail = input.WorkEmail;
            employee.PersonalEmail = input.PersonalEmail;
            employee.MobileNumber = input.MobileNumber;
            employee.WorkNumber = input.WorkNumber;
            employee.Address = input.Address;
            employee.City = input.City;
            employee.Country = input.Country;
            employee.NextOfKinName = input.NextOfKinName;
            employee.NextOfKinRelationship = input.NextOfKinRelationship;
            employee.NextOfKinPhone = input.NextOfKinPhone;
            employee.EmergencyContactName = input.EmergencyContactName;
            employee.EmergencyContactPhone = input.EmergencyContactPhone;
            employee.JobTitle = input.JobTitle;
            employee.DepartmentId = input.DepartmentId;
            employee.ManagerId = input.ManagerId;
            employee.EmploymentType = input.EmploymentType;
            employee.Status = input.Status;
            employee.Grade = input.Grade;
            employee.Location = input.Location;
            employee.HireDate = input.HireDate;
            employee.ProbationEndDate = input.ProbationEndDate;
            employee.ContractEndDate = input.ContractEndDate;
            employee.TerminationDate = input.TerminationDate;
            employee.TerminationReason = input.TerminationReason;
            employee.Client = input.Client;
            employee.BankName = input.BankName;
            employee.BankAccountNumber = input.BankAccountNumber;
            employee.TaxNumber = input.TaxNumber;
            employee.SocialSecurityNumber = input.SocialSecurityNumber;
            employee.Notes = input.Notes;
            employee.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", nameof(Employee), employee.Id,
                changes.Count > 0
                    ? $"{employee.EmployeeNumber} — {string.Join("; ", changes)}"
                    : $"{employee.EmployeeNumber} — details amended");

            TempData["Success"] = $"{employee.FullName} updated.";
            return RedirectToAction(nameof(Details), new { id = employee.Id });
        }

        /// <summary>The fields whose change is worth spelling out in the audit trail.</summary>
        private static List<string> DescribeChanges(Employee before, Employee after)
        {
            var changes = new List<string>();

            void Compare(string label, object? oldValue, object? newValue)
            {
                var a = oldValue?.ToString() ?? "";
                var b = newValue?.ToString() ?? "";
                if (a != b) changes.Add($"{label}: {(a.Length == 0 ? "—" : a)} → {(b.Length == 0 ? "—" : b)}");
            }

            Compare("job title", before.JobTitle, after.JobTitle);
            Compare("department", before.DepartmentId, after.DepartmentId);
            Compare("manager", before.ManagerId, after.ManagerId);
            Compare("status", before.Status, after.Status);
            Compare("employment type", before.EmploymentType, after.EmploymentType);
            Compare("grade", before.Grade, after.Grade);
            Compare("termination date", before.TerminationDate?.ToString("d"), after.TerminationDate?.ToString("d"));
            return changes;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Details
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// One person, everything on record. This is the page the employee register exists to make
        /// possible — before it, these four things could not be shown together.
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            var employee = await _db.Employees.AsNoTracking()
                .Include(e => e.Department).Include(e => e.Manager).Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            ViewBag.DirectReports = await _db.Employees.AsNoTracking()
                .Where(e => e.ManagerId == id)
                .OrderBy(e => e.LastName)
                .ToListAsync();

            ViewBag.TalentAssessments = await _db.TalentIdentifications.AsNoTracking()
                .Where(t => t.EmployeeId == id)
                .OrderByDescending(t => t.CreatedDate).ToListAsync();
            ViewBag.ExitInterviews = await _db.ExitInterviews.AsNoTracking()
                .Where(t => t.EmployeeId == id)
                .OrderByDescending(t => t.CreatedDate).ToListAsync();
            ViewBag.StayInterviews = await _db.EngagementStayInterviews.AsNoTracking()
                .Where(t => t.EmployeeId == id)
                .OrderByDescending(t => t.CreatedDate).ToListAsync();

            // Exit clearance still keys off the account rather than the employee record.
            ViewBag.Clearances = employee.UserId is int userId
                ? await _db.ExitClearances.AsNoTracking()
                    .Where(c => c.EmployeeId == userId)
                    .OrderByDescending(c => c.CreatedDate).ToListAsync()
                : new List<Models.ExitClearance>();

            ViewBag.Documents = employee.UserId is int docUserId
                ? await _db.EmployeeDocuments.AsNoTracking()
                    .Where(d => d.EmployeeId == docUserId)
                    .OrderByDescending(d => d.CreatedAt).Take(10).ToListAsync()
                : new List<Models.Efm.EmployeeDocument>();

            return View(employee);
        }

        /// <summary>The reporting line, drawn from the employee register rather than the accounts.</summary>
        public async Task<IActionResult> OrgChart(int? rootId)
        {
            var employees = await _db.Employees.AsNoTracking()
                .Include(e => e.Department)
                .Where(e => e.Status == EmploymentStatus.Active
                    || e.Status == EmploymentStatus.OnProbation
                    || e.Status == EmploymentStatus.OnLeave)
                .OrderBy(e => e.LastName)
                .ToListAsync();

            ViewBag.RootId = rootId;
            ViewBag.Unmanaged = employees.Count(e => e.ManagerId == null);
            return View(employees);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Leaving
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Take somebody off the payroll. Kept separate from Edit because it is a decision, not a
        /// field change, and it should read as one in the audit trail.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Terminate(int id, EmploymentStatus status, DateTime terminationDate,
            string? reason, bool deactivateAccount = true)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            if (status is not (EmploymentStatus.Resigned or EmploymentStatus.Terminated
                or EmploymentStatus.Retired or EmploymentStatus.EndOfContract))
            {
                TempData["Error"] = "Choose a leaving status: resigned, terminated, retired or end of contract.";
                return RedirectToAction(nameof(Details), new { id });
            }

            employee.Status = status;
            employee.TerminationDate = terminationDate;
            employee.TerminationReason = reason;
            employee.UpdatedAt = DateTime.Now;

            // Anybody reporting to this person is left without a manager rather than silently
            // pointing at a leaver — that is a decision for HR to make explicitly.
            var orphaned = await _db.Employees.Where(e => e.ManagerId == id).ToListAsync();
            foreach (var report in orphaned) report.ManagerId = null;

            // Closing the account is the usual intent, but it is a separate concern and is opt-out.
            if (deactivateAccount && employee.UserId is int userId)
            {
                var account = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (account != null)
                {
                    account.IsActive = false;
                    // Rotating the stamp invalidates any session the leaver still has open.
                    account.SecurityStamp = Guid.NewGuid().ToString();
                }
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync("Terminated", nameof(Employee), employee.Id,
                $"{employee.EmployeeNumber} — {employee.FullName} left on {terminationDate:d MMM yyyy} " +
                $"({status}){(string.IsNullOrWhiteSpace(reason) ? "" : $": {reason}")}" +
                $"{(orphaned.Count > 0 ? $"; {orphaned.Count} direct report(s) left without a manager" : "")}" +
                $"{(deactivateAccount ? "; account deactivated" : "")}");

            TempData["Success"] = $"{employee.FullName} recorded as {status} from {terminationDate:d MMM yyyy}."
                + (orphaned.Count > 0 ? $" {orphaned.Count} direct report(s) now need a manager." : "");
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>Confirm probation and move the employee to active.</summary>
        [HttpPost]
        public async Task<IActionResult> ConfirmProbation(int id)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            employee.Status = EmploymentStatus.Active;
            employee.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("ProbationConfirmed", nameof(Employee), employee.Id,
                $"{employee.EmployeeNumber} — {employee.FullName} confirmed in post");

            TempData["Success"] = $"{employee.FullName} confirmed in post.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Delete & maintenance
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _db.Employees.AsNoTracking()
                .Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            var vm = new DeleteConfirmationVm
            {
                EntityName = "Employee record",
                Icon = "fa-user",
                RecordTitle = employee.FullName,
                Reference = employee.EmployeeNumber,
                Id = employee.Id,
                Controller = "Employees",
                CancelAction = "Details"
            };
            vm.Add("Job title", employee.JobTitle);
            vm.Add("Department", employee.Department?.Name);
            vm.Add("Status", employee.Status.ToString());
            vm.Add("Hire date", employee.HireDate?.ToString("d MMM yyyy"));
            vm.Add("Length of service", employee.LengthOfService);

            var reports = await _db.Employees.CountAsync(e => e.ManagerId == id);
            if (reports > 0) vm.Consequences.Add($"{reports} direct report(s) will be left without a manager");
            vm.Consequences.Add("Their talent assessments and interviews stay, but stop being linked to a person");
            vm.Consequences.Add("The record is soft-deleted, not destroyed — statutory retention still applies, "
                              + "and an administrator can restore it");
            vm.Consequences.Add("To record somebody leaving, use Terminate instead — that keeps the history intact");

            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            foreach (var report in await _db.Employees.Where(e => e.ManagerId == id).ToListAsync())
                report.ManagerId = null;

            employee.IsDeleted = true;
            employee.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Deleted", nameof(Employee), employee.Id,
                $"{employee.EmployeeNumber} — {employee.FullName} withdrawn from the register (retained)");

            TempData["Success"] = $"{employee.FullName} removed from the register.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Re-run the backfill by hand. Useful after importing accounts, or after correcting a
        /// name so a previously ambiguous historical record can now be matched.
        /// </summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> RunBackfill()
        {
            var result = await _backfill.RunAsync();

            await _audit.LogAsync("BackfillRun", nameof(Employee), null,
                $"{result.EmployeesCreated} record(s) created, {result.TotalLinked} historical row(s) linked, "
                + $"{result.Ambiguous} ambiguous");

            TempData["Success"] =
                $"{result.EmployeesCreated} employee record(s) created and {result.TotalLinked} historical "
                + $"row(s) linked."
                + (result.Ambiguous > 0
                    ? $" {result.Ambiguous} row(s) matched more than one person and were left alone — "
                      + "resolve those by hand."
                    : "");
            return RedirectToAction(nameof(Index));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════════

        private async Task ValidateAsync(Employee input)
        {
            var number = input.EmployeeNumber?.Trim();
            if (!string.IsNullOrEmpty(number))
            {
                var clash = await _db.Employees.IgnoreQueryFilters()
                    .AnyAsync(e => e.EmployeeNumber == number && e.Id != input.Id);
                if (clash) ModelState.AddModelError(nameof(input.EmployeeNumber), "That employee number is already in use.");
            }

            if (input.UserId.HasValue)
            {
                var clash = await _db.Employees.IgnoreQueryFilters()
                    .AnyAsync(e => e.UserId == input.UserId && e.Id != input.Id);
                if (clash) ModelState.AddModelError(nameof(input.UserId), "That account is already linked to another employee.");
            }

            if (input.ManagerId == input.Id && input.Id != 0)
                ModelState.AddModelError(nameof(input.ManagerId), "An employee cannot be their own manager.");

            if (input.ManagerId.HasValue && await WouldLoopAsync(input.Id, input.ManagerId.Value))
                ModelState.AddModelError(nameof(input.ManagerId),
                    "That would create a circular reporting line — the chosen manager already reports to this employee.");

            if (input.TerminationDate.HasValue && input.HireDate.HasValue && input.TerminationDate < input.HireDate)
                ModelState.AddModelError(nameof(input.TerminationDate), "The termination date cannot be before the hire date.");

            if (input.DateOfBirth.HasValue && input.DateOfBirth > DateTime.Today.AddYears(-14))
                ModelState.AddModelError(nameof(input.DateOfBirth), "Check the date of birth — that would make the employee under 14.");
        }

        /// <summary>True when making <paramref name="managerId"/> the manager would close a loop.</summary>
        private async Task<bool> WouldLoopAsync(int employeeId, int managerId)
        {
            if (employeeId == 0) return false;

            var chain = await _db.Employees.AsNoTracking()
                .Select(e => new { e.Id, e.ManagerId }).ToListAsync();

            var current = (int?)managerId;
            var guard = 0;
            while (current.HasValue && guard++ < 100)
            {
                if (current.Value == employeeId) return true;
                current = chain.FirstOrDefault(c => c.Id == current.Value)?.ManagerId;
            }
            return false;
        }

        private async Task<string> NextEmployeeNumberAsync()
        {
            var existing = await _db.Employees.IgnoreQueryFilters()
                .Where(e => e.EmployeeNumber.StartsWith("EMP-"))
                .Select(e => e.EmployeeNumber).ToListAsync();

            var next = existing
                .Select(n => int.TryParse(n[4..], out var v) ? v : 0)
                .DefaultIfEmpty(0).Max() + 1;
            return $"EMP-{next:D5}";
        }

        private async Task PopulateListsAsync(int? excludeId = null)
        {
            ViewBag.Departments = await _db.Departments.AsNoTracking()
                .OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToListAsync();

            ViewBag.Managers = await _db.Employees.AsNoTracking()
                .Where(e => excludeId == null || e.Id != excludeId)
                .OrderBy(e => e.LastName)
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName + " — " + e.JobTitle })
                .ToListAsync();

            // Only accounts not already claimed by another employee can be linked.
            var linked = await _db.Employees.AsNoTracking()
                .Where(e => e.UserId != null && (excludeId == null || e.Id != excludeId))
                .Select(e => e.UserId!.Value).ToListAsync();

            ViewBag.Accounts = await _db.Users.AsNoTracking()
                .Where(u => !linked.Contains(u.Id))
                .OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName + " (" + u.Email + ")" })
                .ToListAsync();
        }
    }
}
