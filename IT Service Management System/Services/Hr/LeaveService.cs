using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers.Hr;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// The leave engine: works out how many days a request costs, whether the employee may take
    /// it, and keeps balances and the ledger in step.
    /// <para>
    /// Balances are held rather than derived. A derived balance cannot be corrected, cannot carry
    /// an opening figure from a migration, and silently rewrites history the moment a rule changes.
    /// Every movement is written to <see cref="LeaveLedgerEntry"/> as well, because an unused
    /// vacation balance is paid out on termination and therefore has to be defensible.
    /// </para>
    /// </summary>
    public class LeaveService
    {
        private readonly ApplicationDbContext _db;
        private readonly StatutoryService _statutory;
        private readonly ILogger<LeaveService> _log;

        public LeaveService(ApplicationDbContext db, StatutoryService statutory, ILogger<LeaveService> log)
        {
            _db = db; _statutory = statutory; _log = log;
        }

        // ── Costing a request ────────────────────────────────────────────────────

        /// <summary>
        /// How many days a date range costs against a leave type. Working-day types skip weekends
        /// and public holidays; calendar-day types (maternity) count every day.
        /// </summary>
        public async Task<decimal> CalculateDaysAsync(LeaveType type, DateTime start, DateTime end, bool isHalfDay)
        {
            if (end < start) return 0;

            if (isHalfDay)
            {
                // A half day only makes sense on a single date.
                return start.Date == end.Date ? 0.5m : 0m;
            }

            if (!type.CountsWorkingDaysOnly)
                return (end.Date - start.Date).Days + 1;

            return await _statutory.WorkingDaysBetweenAsync(start, end);
        }

        // ── Eligibility ──────────────────────────────────────────────────────────

        /// <summary>
        /// Everything that would stop this request being granted, in one pass, so the employee sees
        /// all of it at once rather than fixing one problem to discover the next.
        /// </summary>
        public async Task<LeaveCheck> CheckAsync(Employee employee, LeaveType type,
            DateTime start, DateTime end, decimal days, int? excludeRequestId = null)
        {
            var check = new LeaveCheck();

            if (end < start)
                check.Errors.Add("The last day cannot be before the first day.");

            if (days <= 0)
                check.Errors.Add("That range contains no chargeable days — it may fall entirely on "
                                 + "weekends or public holidays.");

            // ── Qualifying service ──
            if (type.QualifyingMonths > 0)
            {
                var (qualifies, short_) = ZimbabweLabourLaw.QualifiesForMaternityLeave(
                    employee.HireDate, start, type.QualifyingMonths);

                if (!qualifies)
                    check.Errors.Add($"{type.Name} requires {type.QualifyingMonths} months of service. "
                                   + $"This employee is {short_} month(s) short as at {start:d MMM yyyy}.");
            }

            // ── Sex-specific entitlements ──
            if (!string.IsNullOrWhiteSpace(type.RestrictedToGender)
                && !string.IsNullOrWhiteSpace(employee.Gender)
                && !string.Equals(type.RestrictedToGender, employee.Gender, StringComparison.OrdinalIgnoreCase))
            {
                check.Errors.Add($"{type.Name} is recorded as applying to {type.RestrictedToGender} employees.");
            }

            // ── Overlap with leave already booked ──
            var overlapping = await _db.LeaveRequests.AsNoTracking()
                .Include(r => r.LeaveType)
                .Where(r => r.EmployeeId == employee.Id
                         && r.Id != (excludeRequestId ?? 0)
                         && r.Status != LeaveRequestStatus.Rejected
                         && r.Status != LeaveRequestStatus.Cancelled
                         && r.StartDate <= end && r.EndDate >= start)
                .ToListAsync();

            foreach (var clash in overlapping)
                check.Errors.Add($"Overlaps {clash.Reference} — {clash.LeaveType?.Name} "
                               + $"from {clash.StartDate:d MMM} to {clash.EndDate:d MMM}.");

            // ── Balance ──
            var balance = await GetOrCreateBalanceAsync(employee.Id, type.Id, start.Year);

            if (type.HasHalfPayTier)
            {
                // Sick leave does not fail on balance; it steps down to half pay and then to
                // unpaid, which is what s.14 provides for.
                var split = ZimbabweLabourLaw.SplitSickLeave(
                    (int)Math.Ceiling(days),
                    (int)balance.Taken,
                    (int)balance.HalfPayTaken,
                    (int)type.AnnualEntitlementDays,
                    type.HalfPayDays);

                check.FullPayDays = split.FullPayDays;
                check.HalfPayDays = split.HalfPayDays;
                check.UnpaidDays = split.UnpaidDays;

                if (split.HalfPayDays > 0)
                    check.Warnings.Add($"{split.HalfPayDays} day(s) of this fall on half pay — the "
                                     + "full-pay entitlement is exhausted.");

                if (split.UnpaidDays > 0)
                    check.Warnings.Add($"{split.UnpaidDays} day(s) are unpaid. Both statutory sick-leave "
                                     + "entitlements are exhausted, and the incapacity provisions of "
                                     + "s.14 may now apply.");
            }
            else if (type.IsPaid)
            {
                check.FullPayDays = days;

                if (days > balance.Available)
                    check.Errors.Add($"Only {balance.Available:0.##} day(s) available — "
                                   + $"{days:0.##} requested.");
            }
            else
            {
                check.UnpaidDays = days;
            }

            // ── Supporting document ──
            if (type.RequiresMedicalCertificate && days >= type.CertificateRequiredAfterDays)
                check.RequiresDocument = true;

            // ── Notice ──
            if (type.NoticeDaysRequired > 0)
            {
                var noticeGiven = (start.Date - DateTime.Today).TotalDays;
                if (noticeGiven < type.NoticeDaysRequired)
                    check.Warnings.Add($"{type.Name} normally needs {type.NoticeDaysRequired} day(s) "
                                     + $"notice; this gives {Math.Max(0, noticeGiven):0}.");
            }

            check.Balance = balance;
            return check;
        }

        public class LeaveCheck
        {
            public List<string> Errors { get; } = new();
            public List<string> Warnings { get; } = new();
            public decimal FullPayDays { get; set; }
            public decimal HalfPayDays { get; set; }
            public decimal UnpaidDays { get; set; }
            public bool RequiresDocument { get; set; }
            public LeaveBalance? Balance { get; set; }

            public bool IsAllowed => Errors.Count == 0;
        }

        // ── Balances ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The employee's balance for a type and cycle, created with its opening entitlement if it
        /// does not exist yet.
        /// </summary>
        public async Task<LeaveBalance> GetOrCreateBalanceAsync(int employeeId, int leaveTypeId, int cycleYear)
        {
            var balance = await _db.LeaveBalances
                .FirstOrDefaultAsync(b => b.EmployeeId == employeeId
                                       && b.LeaveTypeId == leaveTypeId
                                       && b.CycleYear == cycleYear);
            if (balance != null) return balance;

            var type = await _db.LeaveTypes.AsNoTracking().FirstAsync(t => t.Id == leaveTypeId);
            var employee = await _db.Employees.AsNoTracking().FirstAsync(e => e.Id == employeeId);

            balance = new LeaveBalance
            {
                EmployeeId = employeeId,
                LeaveTypeId = leaveTypeId,
                CycleYear = cycleYear,
                // A type that accrues starts empty and fills up month by month; a type that is
                // granted starts with the whole allowance.
                Accrued = type.AccrualPerMonth > 0
                    ? AccruedToDate(employee.HireDate, cycleYear, type.AccrualPerMonth)
                    : type.AnnualEntitlementDays
            };

            _db.LeaveBalances.Add(balance);
            await _db.SaveChangesAsync();

            await WriteLedgerAsync(balance, LeaveLedgerKind.OpeningBalance, balance.Accrued,
                $"Opening entitlement for {cycleYear}");

            return balance;
        }

        /// <summary>
        /// Accrual earned so far in a cycle. Service starting mid-cycle accrues only from the hire
        /// date, and accrual stops at today rather than running to the end of the year — an
        /// employee cannot spend leave they have not yet earned.
        /// </summary>
        private static decimal AccruedToDate(DateTime? hireDate, int cycleYear, decimal perMonth)
        {
            var cycleStart = new DateTime(cycleYear, 1, 1);
            var cycleEnd = new DateTime(cycleYear, 12, 31);

            var from = hireDate.HasValue && hireDate.Value > cycleStart ? hireDate.Value : cycleStart;
            var to = DateTime.Today < cycleEnd ? DateTime.Today : cycleEnd;

            return ZimbabweLabourLaw.AccruedVacationLeave(from, to, perMonth);
        }

        /// <summary>
        /// Bring every accruing balance up to date. Run monthly; idempotent, because it computes
        /// what the accrual should be and only writes the difference.
        /// </summary>
        public async Task<int> RunAccrualAsync(int cycleYear, CancellationToken ct = default)
        {
            var accruingTypes = await _db.LeaveTypes.AsNoTracking()
                .Where(t => t.IsActive && t.AccrualPerMonth > 0)
                .ToListAsync(ct);
            if (accruingTypes.Count == 0) return 0;

            var employees = await _db.Employees.AsNoTracking()
                .Where(e => e.Status == EmploymentStatus.Active
                         || e.Status == EmploymentStatus.OnProbation
                         || e.Status == EmploymentStatus.OnLeave)
                .Select(e => new { e.Id, e.HireDate })
                .ToListAsync(ct);

            var updated = 0;

            foreach (var type in accruingTypes)
            {
                foreach (var employee in employees)
                {
                    var expected = AccruedToDate(employee.HireDate, cycleYear, type.AccrualPerMonth);

                    var balance = await _db.LeaveBalances
                        .FirstOrDefaultAsync(b => b.EmployeeId == employee.Id
                                               && b.LeaveTypeId == type.Id
                                               && b.CycleYear == cycleYear, ct);

                    if (balance == null)
                    {
                        balance = new LeaveBalance
                        {
                            EmployeeId = employee.Id,
                            LeaveTypeId = type.Id,
                            CycleYear = cycleYear,
                            Accrued = expected
                        };
                        _db.LeaveBalances.Add(balance);
                        updated++;
                        continue;
                    }

                    var difference = expected - balance.Accrued;
                    if (difference <= 0) continue;   // never claw back accrual already granted

                    balance.Accrued = expected;
                    balance.UpdatedAt = DateTime.Now;
                    await WriteLedgerAsync(balance, LeaveLedgerKind.Accrual, difference,
                        $"Monthly accrual to {DateTime.Today:d MMM yyyy}", save: false);
                    updated++;
                }
            }

            if (updated > 0) await _db.SaveChangesAsync(ct);
            _log.LogInformation("Leave accrual for {Year}: {Count} balance(s) updated.", cycleYear, updated);
            return updated;
        }

        /// <summary>
        /// Close a cycle: carry over what the type allows, and forfeit the rest. Both movements are
        /// written to the ledger, because "where did my leave go" is a question that gets asked.
        /// </summary>
        public async Task<int> CloseCycleAsync(int cycleYear, CancellationToken ct = default)
        {
            var types = await _db.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);

            var balances = await _db.LeaveBalances
                .Where(b => b.CycleYear == cycleYear)
                .ToListAsync(ct);

            var processed = 0;

            foreach (var balance in balances)
            {
                if (!types.TryGetValue(balance.LeaveTypeId, out var type)) continue;

                var remaining = balance.Available;
                if (remaining <= 0) continue;

                var carried = Math.Min(remaining, type.MaxCarryOverDays);
                var forfeited = remaining - carried;

                if (carried > 0)
                {
                    var next = await GetOrCreateBalanceAsync(balance.EmployeeId, balance.LeaveTypeId, cycleYear + 1);
                    next.OpeningBalance += carried;
                    next.UpdatedAt = DateTime.Now;
                    await WriteLedgerAsync(next, LeaveLedgerKind.CarriedOver, carried,
                        $"Carried over from {cycleYear}", save: false);
                }

                if (forfeited > 0)
                {
                    await WriteLedgerAsync(balance, LeaveLedgerKind.Forfeited, -forfeited,
                        $"Forfeited at the close of {cycleYear} — "
                        + $"carry-over capped at {type.MaxCarryOverDays:0.##} day(s)", save: false);
                }

                processed++;
            }

            await _db.SaveChangesAsync(ct);
            return processed;
        }

        // ── Applying a decision to the balance ───────────────────────────────────

        /// <summary>Hold days against the balance while a request is awaiting a decision.</summary>
        public async Task ReserveAsync(LeaveRequest request)
        {
            var balance = await GetOrCreateBalanceAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
            balance.Pending += request.Days;
            balance.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        /// <summary>Move a request's days from pending to booked once it is approved.</summary>
        public async Task CommitAsync(LeaveRequest request)
        {
            var balance = await GetOrCreateBalanceAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);

            balance.Pending = Math.Max(0, balance.Pending - request.Days);
            balance.Booked += request.Days;
            if (request.HalfPayDays > 0) balance.HalfPayTaken += request.HalfPayDays;
            balance.UpdatedAt = DateTime.Now;

            await WriteLedgerAsync(balance, LeaveLedgerKind.Taken, -request.Days,
                $"{request.Reference} — {request.StartDate:d MMM} to {request.EndDate:d MMM}", request.Id);
        }

        /// <summary>Give the days back when a request is rejected or cancelled.</summary>
        public async Task ReleaseAsync(LeaveRequest request, bool wasApproved)
        {
            var balance = await GetOrCreateBalanceAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);

            if (wasApproved)
            {
                balance.Booked = Math.Max(0, balance.Booked - request.Days);
                if (request.HalfPayDays > 0)
                    balance.HalfPayTaken = Math.Max(0, balance.HalfPayTaken - request.HalfPayDays);

                await WriteLedgerAsync(balance, LeaveLedgerKind.Cancelled, request.Days,
                    $"{request.Reference} cancelled", request.Id, save: false);
            }
            else
            {
                balance.Pending = Math.Max(0, balance.Pending - request.Days);
            }

            balance.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        /// <summary>Move booked days to taken once the leave has actually been served.</summary>
        public async Task<int> MarkTakenAsync(CancellationToken ct = default)
        {
            var due = await _db.LeaveRequests
                .Where(r => r.Status == LeaveRequestStatus.Approved && r.EndDate < DateTime.Today)
                .ToListAsync(ct);

            foreach (var request in due)
            {
                var balance = await GetOrCreateBalanceAsync(request.EmployeeId, request.LeaveTypeId, request.StartDate.Year);
                balance.Booked = Math.Max(0, balance.Booked - request.Days);
                balance.Taken += request.Days;
                balance.UpdatedAt = DateTime.Now;

                request.Status = LeaveRequestStatus.Taken;
                request.UpdatedAt = DateTime.Now;
            }

            if (due.Count > 0) await _db.SaveChangesAsync(ct);
            return due.Count;
        }

        /// <summary>
        /// What an unused balance is worth on termination, for the types the contract says are paid
        /// out. Vacation leave normally is; sick leave normally is not.
        /// </summary>
        public async Task<List<LeavePayout>> CalculateTerminationPayoutAsync(int employeeId, decimal monthlySalary)
        {
            var year = DateTime.Today.Year;
            var dailyRate = monthlySalary > 0 ? monthlySalary / 22m : 0m;   // 22 working days a month

            var balances = await _db.LeaveBalances.AsNoTracking()
                .Include(b => b.LeaveType)
                .Where(b => b.EmployeeId == employeeId && b.CycleYear == year
                         && b.LeaveType!.PaidOutOnTermination)
                .ToListAsync();

            return balances
                .Where(b => b.Available > 0)
                .Select(b => new LeavePayout(
                    b.LeaveType!.Name,
                    b.Available,
                    Math.Round(dailyRate, 2),
                    Math.Round(b.Available * dailyRate, 2)))
                .ToList();
        }

        public record LeavePayout(string LeaveType, decimal Days, decimal DailyRate, decimal Amount);

        // ── Ledger ───────────────────────────────────────────────────────────────

        private async Task WriteLedgerAsync(LeaveBalance balance, LeaveLedgerKind kind, decimal days,
            string narrative, int? requestId = null, bool save = true)
        {
            _db.LeaveLedgerEntries.Add(new LeaveLedgerEntry
            {
                EmployeeId = balance.EmployeeId,
                LeaveTypeId = balance.LeaveTypeId,
                LeaveRequestId = requestId,
                CycleYear = balance.CycleYear,
                Kind = kind,
                Days = days,
                BalanceAfter = balance.Available,
                Narrative = narrative,
                At = DateTime.Now
            });

            if (save) await _db.SaveChangesAsync();
        }
    }
}
