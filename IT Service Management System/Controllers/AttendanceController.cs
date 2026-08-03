using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Hr;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Hr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Attendance and overtime — clocking, the daily register, shift patterns, and the overtime
    /// approval that has to happen before payroll will pay for it.
    /// <para>
    /// Open to every role for their own clocking and timesheet; the register, corrections and
    /// approvals are gated at the action.
    /// </para>
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager",
                   "ProjectManager", "TeamLead", "Finance", "Procurement", "Employee",
                   "SupportAgent", "Development", "QualityManager")]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AttendanceService _attendance;
        private readonly StatutoryService _statutory;
        private readonly AuditService _audit;

        public AttendanceController(ApplicationDbContext db, AttendanceService attendance,
            StatutoryService statutory, AuditService audit)
        {
            _db = db; _attendance = attendance; _statutory = statutory; _audit = audit;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool IsHr => Roles.IsFullAccess(Role) || Role == Roles.HR;
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        private const int PageSize = 31;

        private async Task<Employee?> MeAsync() =>
            await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == Uid);

        /// <summary>The line manager of the employee, from the register.</summary>
        private async Task<bool> ManagesAsync(int employeeId, Employee? me)
        {
            if (me == null) return false;
            var managerId = await _db.Employees.AsNoTracking()
                .Where(e => e.Id == employeeId).Select(e => e.ManagerId).FirstOrDefaultAsync();
            return managerId == me.Id;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  My attendance
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index(DateTime? month)
        {
            var me = await MeAsync();
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            var anchor = new DateTime((month ?? DateTime.Today).Year, (month ?? DateTime.Today).Month, 1);
            var from = anchor;
            var to = anchor.AddMonths(1).AddDays(-1);

            ViewBag.Employee = me;
            ViewBag.Anchor = anchor;
            ViewBag.Shift = await _attendance.ShiftForAsync(me.Id, DateTime.Today);
            ViewBag.Summary = await _attendance.SummariseAsync(me.Id, from, to);

            ViewBag.Records = await _db.AttendanceRecords.AsNoTracking()
                .Include(a => a.Shift).Include(a => a.LeaveRequest).ThenInclude(l => l!.LeaveType)
                .Where(a => a.EmployeeId == me.Id && a.Date >= from && a.Date <= to)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            // Today's row drives the clock-in/clock-out button.
            ViewBag.Today = await _db.AttendanceRecords.AsNoTracking()
                .FirstOrDefaultAsync(a => a.EmployeeId == me.Id && a.Date == DateTime.Today);

            ViewBag.MyOvertime = await _db.OvertimeRequests.AsNoTracking()
                .Where(a => a.EmployeeId == me.Id && a.Date >= from && a.Date <= to)
                .OrderByDescending(a => a.Date).ToListAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ClockIn()
        {
            var me = await MeAsync();
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            var result = await _attendance.ClockInAsync(me.Id, DateTime.Now, Uid);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

            if (result.Succeeded)
                await _audit.LogAsync("ClockIn", nameof(AttendanceRecord), result.Record?.Id,
                    $"{me.FullName} clocked in at {DateTime.Now:HH:mm}");

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ClockOut()
        {
            var me = await MeAsync();
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            var result = await _attendance.ClockOutAsync(me.Id, DateTime.Now, Uid);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

            if (result.Succeeded)
                await _audit.LogAsync("ClockOut", nameof(AttendanceRecord), result.Record?.Id,
                    $"{me.FullName} clocked out at {DateTime.Now:HH:mm} — "
                    + $"{result.Record?.HoursWorked:0.##} hour(s)");

            return RedirectToAction(nameof(Index));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Daily register
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Everyone's attendance for a single day — the supervisor's morning view.</summary>
        [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager", "TeamLead")]
        public async Task<IActionResult> Register(DateTime? date, int? departmentId, AttendanceStatus? status)
        {
            var day = (date ?? DateTime.Today).Date;

            IQueryable<AttendanceRecord> query = _db.AttendanceRecords.AsNoTracking()
                .Include(a => a.Employee).ThenInclude(e => e!.Department)
                .Include(a => a.Shift).Include(a => a.LeaveRequest).ThenInclude(l => l!.LeaveType)
                .Where(a => a.Date == day);

            if (departmentId.HasValue) query = query.Where(a => a.Employee!.DepartmentId == departmentId.Value);
            if (status.HasValue) query = query.Where(a => a.Status == status.Value);

            var records = await query.OrderBy(a => a.Employee!.LastName).ToListAsync();

            ViewBag.Date = day;
            ViewBag.DepartmentId = departmentId;
            ViewBag.Status = status;
            ViewBag.IsHoliday = (await _statutory.PublicHolidaysAsync(day, day)).Contains(day);
            ViewBag.HolidayName = await _db.PublicHolidays.AsNoTracking()
                .Where(h => h.Date == day).Select(h => h.Name).FirstOrDefaultAsync();
            ViewBag.Departments = await _db.Departments.AsNoTracking()
                .OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToListAsync();

            // How many employees have no record at all — the reconciliation has not been run.
            var headcount = await _db.Employees.CountAsync(e =>
                e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.OnProbation
                || e.Status == EmploymentStatus.OnLeave);
            ViewBag.Headcount = headcount;
            ViewBag.Unrecorded = Math.Max(0, headcount - await _db.AttendanceRecords.CountAsync(a => a.Date == day));

            return View(records);
        }

        /// <summary>
        /// Create the missing rows for a day. Anybody who did not clock gets a record saying why,
        /// so an absence is something recorded rather than something missing.
        /// </summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager")]
        public async Task<IActionResult> Reconcile(DateTime date)
        {
            var created = await _attendance.ReconcileDayAsync(date);
            var incomplete = await _attendance.FlagIncompleteAsync();

            await _audit.LogAsync("Reconciled", nameof(AttendanceRecord), null,
                $"{date:d MMM yyyy}: {created} record(s) created, {incomplete} incomplete day(s) flagged");

            TempData["Success"] = created > 0
                ? $"{created} record(s) created for {date:d MMM yyyy}."
                : $"Every employee already has a record for {date:d MMM yyyy}.";

            if (incomplete > 0)
                TempData["Warning"] = $"{incomplete} day(s) have a clock-in but no clock-out and need "
                                    + "a finishing time before they can be approved.";

            return RedirectToAction(nameof(Register), new { date });
        }

        /// <summary>
        /// Correct a day's times. Flagged as a manual entry, because a keyed record that feeds
        /// overtime pay must be distinguishable from a clocked one.
        /// </summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager", "TeamLead")]
        public async Task<IActionResult> Correct(int id, DateTime? clockIn, DateTime? clockOut,
            int breakMinutes, AttendanceStatus status, string? notes)
        {
            var record = await _db.AttendanceRecords.Include(a => a.Shift)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && !await ManagesAsync(record.EmployeeId, me)) return AccessDenied();

            if (record.IsApproved && !IsHr)
            {
                TempData["Error"] = "That day has been approved. Ask HR to reopen it.";
                return RedirectToAction(nameof(Register), new { date = record.Date });
            }

            if (clockIn.HasValue && clockOut.HasValue && clockOut <= clockIn)
            {
                TempData["Error"] = "The finishing time must be after the starting time.";
                return RedirectToAction(nameof(Register), new { date = record.Date });
            }

            var before = $"{record.ClockIn:HH:mm}–{record.ClockOut:HH:mm}, {record.HoursWorked:0.##}h";

            record.ClockIn = clockIn;
            record.ClockOut = clockOut;
            record.BreakMinutes = Math.Max(0, breakMinutes);
            record.Status = status;
            record.Notes = notes;
            record.IsManualEntry = true;
            record.RecordedById = Uid;
            record.UpdatedAt = DateTime.Now;

            // Overtime changed by hand needs signing off again before payroll takes it.
            record.IsApproved = false;
            record.ApprovedById = null;
            record.ApprovedAt = null;

            AttendanceService.Recalculate(record);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Corrected", nameof(AttendanceRecord), id,
                $"{record.Date:d MMM yyyy} for employee #{record.EmployeeId}: {before} → "
                + $"{record.ClockIn:HH:mm}–{record.ClockOut:HH:mm}, {record.HoursWorked:0.##}h ({status})");

            TempData["Success"] = $"{record.Date:d MMM yyyy} corrected — {record.HoursWorked:0.##} hour(s).";
            return RedirectToAction(nameof(Register), new { date = record.Date });
        }

        /// <summary>
        /// Approve days. Overtime is paid at a premium, so it is signed off before payroll can
        /// value it — an unapproved day contributes nothing.
        /// </summary>
        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager", "TeamLead")]
        public async Task<IActionResult> ApproveDays(int[] recordIds, DateTime date)
        {
            if (recordIds.Length == 0)
            {
                TempData["Error"] = "Select at least one day.";
                return RedirectToAction(nameof(Register), new { date });
            }

            var me = await MeAsync();
            var records = await _db.AttendanceRecords
                .Where(a => recordIds.Contains(a.Id) && !a.IsApproved)
                .ToListAsync();

            var approved = 0;
            foreach (var record in records)
            {
                if (!IsHr && !await ManagesAsync(record.EmployeeId, me)) continue;

                // A day with a clock-in and no clock-out has no defensible hours figure.
                if (record.IsIncomplete) continue;

                record.IsApproved = true;
                record.ApprovedById = me?.Id;
                record.ApprovedAt = DateTime.Now;
                approved++;
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync("Approved", nameof(AttendanceRecord), null,
                $"{approved} attendance day(s) approved for {date:d MMM yyyy}");

            TempData["Success"] = $"{approved} day(s) approved.";
            if (approved < recordIds.Length)
                TempData["Warning"] = $"{recordIds.Length - approved} day(s) were skipped — either "
                                    + "incomplete, or not yours to approve.";

            return RedirectToAction(nameof(Register), new { date });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Timesheets
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>One employee over a period, with the overtime split by rate.</summary>
        public async Task<IActionResult> Timesheet(int employeeId, DateTime? from, DateTime? to)
        {
            var me = await MeAsync();
            var isManager = await ManagesAsync(employeeId, me);
            if (!IsHr && !isManager && (me == null || me.Id != employeeId)) return AccessDenied();

            var start = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = to ?? start.AddMonths(1).AddDays(-1);

            var employee = await _db.Employees.AsNoTracking()
                .Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return NotFound();

            ViewBag.Employee = employee;
            ViewBag.From = start; ViewBag.To = end;
            ViewBag.Summary = await _attendance.SummariseAsync(employeeId, start, end);
            ViewBag.CanApprove = IsHr || isManager;

            return View(await _db.AttendanceRecords.AsNoTracking()
                .Include(a => a.Shift).Include(a => a.LeaveRequest).ThenInclude(l => l!.LeaveType)
                .Include(a => a.ApprovedBy)
                .Where(a => a.EmployeeId == employeeId && a.Date >= start && a.Date <= end)
                .OrderBy(a => a.Date)
                .ToListAsync());
        }

        /// <summary>
        /// What approved overtime is worth for a period, at the multipliers in force. This is the
        /// figure payroll picks up.
        /// </summary>
        [RoleAuthorize("Admin", "SystemsAdmin", "HR", "Finance")]
        public async Task<IActionResult> OvertimeValuation(DateTime? from, DateTime? to)
        {
            var start = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = to ?? start.AddMonths(1).AddDays(-1);

            var valuations = await _attendance.ValueOvertimeAsync(start, end, end);
            var ids = valuations.Select(v => v.EmployeeId).ToList();

            ViewBag.From = start; ViewBag.To = end;
            ViewBag.Employees = await _db.Employees.AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e);

            ViewBag.Multipliers = await _statutory.ValuesAsync(new[]
            {
                StatutoryKeys.OvertimeMultiplier,
                StatutoryKeys.RestDayMultiplier,
                StatutoryKeys.PublicHolidayMultiplier,
                StatutoryKeys.StandardHoursPerWeek
            }, end);

            // Anything unapproved is excluded from the valuation, so say how much is being left out.
            ViewBag.UnapprovedHours = await _db.AttendanceRecords.AsNoTracking()
                .Where(a => a.Date >= start && a.Date <= end && !a.IsApproved)
                .SumAsync(a => (decimal?)(a.OvertimeHours + a.RestDayHours + a.PublicHolidayHours)) ?? 0m;

            return View(valuations);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Overtime requests
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> RequestOvertime(DateTime date, decimal hours, string reason)
        {
            var me = await MeAsync();
            if (me == null) return View("~/Views/Leave/NoEmployeeRecord.cshtml");

            if (hours <= 0 || hours > 16)
            {
                TempData["Error"] = "Hours must be between 0 and 16.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Give a reason — overtime is paid at a premium and has to be justified.";
                return RedirectToAction(nameof(Index));
            }

            // Work out the rate band up front so the approver sees what they are agreeing to.
            var shift = await _attendance.ShiftForAsync(me.Id, date);
            var isHoliday = (await _statutory.PublicHolidaysAsync(date, date)).Contains(date.Date);
            var worksThatDay = shift?.WorksOn(date.DayOfWeek)
                ?? date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

            var request = new OvertimeRequest
            {
                EmployeeId = me.Id,
                Date = date.Date,
                HoursRequested = hours,
                DayType = isHoliday ? DayType.PublicHoliday
                        : worksThatDay ? DayType.WorkingDay : DayType.RestDay,
                Reason = reason.Trim(),
                RequestedById = Uid
            };

            _db.OvertimeRequests.Add(request);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Requested", nameof(OvertimeRequest), request.Id,
                $"{request.Reference} — {hours:0.##} hour(s) on {date:d MMM yyyy} ({request.DayType})");

            TempData["Success"] = $"{request.Reference} submitted for approval.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Overtime waiting on the signed-in manager, and on HR.</summary>
        [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager", "TeamLead")]
        public async Task<IActionResult> OvertimeQueue()
        {
            var me = await MeAsync();

            var pending = await _db.OvertimeRequests.AsNoTracking()
                .Include(o => o.Employee).ThenInclude(e => e!.Department)
                .Where(o => o.Status == OvertimeStatus.Requested)
                .OrderBy(o => o.Date)
                .ToListAsync();

            // A manager sees their own team; HR sees everything.
            if (!IsHr && me != null)
            {
                var mine = await _db.Employees.AsNoTracking()
                    .Where(e => e.ManagerId == me.Id).Select(e => e.Id).ToListAsync();
                pending = pending.Where(o => mine.Contains(o.EmployeeId)).ToList();
            }

            ViewBag.Decided = await _db.OvertimeRequests.AsNoTracking()
                .Include(o => o.Employee).Include(o => o.ApprovedBy)
                .Where(o => o.Status != OvertimeStatus.Requested)
                .OrderByDescending(o => o.ApprovedAt ?? o.CreatedAt)
                .Take(25).ToListAsync();

            return View(pending);
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR", "GeneralManager", "DepartmentManager", "TeamLead")]
        public async Task<IActionResult> DecideOvertime(int id, bool approve, decimal? hoursApproved, string? note)
        {
            var request = await _db.OvertimeRequests
                .Include(o => o.Employee).FirstOrDefaultAsync(o => o.Id == id);
            if (request == null) return NotFound();

            var me = await MeAsync();
            if (!IsHr && !await ManagesAsync(request.EmployeeId, me)) return AccessDenied();

            // Nobody approves their own overtime.
            if (me != null && me.Id == request.EmployeeId && !Roles.IsFullAccess(Role))
            {
                TempData["Error"] = "You cannot approve your own overtime.";
                return RedirectToAction(nameof(OvertimeQueue));
            }

            if (!approve && string.IsNullOrWhiteSpace(note))
            {
                TempData["Error"] = "Give a reason when rejecting overtime.";
                return RedirectToAction(nameof(OvertimeQueue));
            }

            if (!request.IsOpen)
            {
                TempData["Error"] = $"{request.Reference} has already been decided.";
                return RedirectToAction(nameof(OvertimeQueue));
            }

            request.Status = approve ? OvertimeStatus.Approved : OvertimeStatus.Rejected;
            // An approver may sanction fewer hours than were asked for.
            request.HoursApproved = approve ? (hoursApproved ?? request.HoursRequested) : 0;
            request.ApprovedById = me?.Id;
            request.ApprovedAt = DateTime.Now;
            request.DecisionNote = note;

            await _db.SaveChangesAsync();

            await _audit.LogAsync(approve ? "Approved" : "Rejected", nameof(OvertimeRequest), id,
                $"{request.Reference} for {request.Employee?.FullName} — "
                + $"{(approve ? $"{request.HoursApproved:0.##} hour(s) approved" : $"rejected: {note}")}");

            TempData["Success"] = approve
                ? $"{request.Reference} approved for {request.HoursApproved:0.##} hour(s)."
                : $"{request.Reference} rejected.";

            return RedirectToAction(nameof(OvertimeQueue));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Shifts
        // ════════════════════════════════════════════════════════════════════════

        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> Shifts()
        {
            ViewBag.Assignments = await _db.ShiftAssignments.AsNoTracking()
                .Include(a => a.Employee).Include(a => a.Shift)
                .Where(a => a.ToDate == null || a.ToDate >= DateTime.Today)
                .OrderBy(a => a.Employee!.LastName)
                .ToListAsync();

            ViewBag.Employees = await _db.Employees.AsNoTracking()
                .Where(e => e.Status == EmploymentStatus.Active || e.Status == EmploymentStatus.OnProbation)
                .OrderBy(e => e.LastName)
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName })
                .ToListAsync();

            ViewBag.StandardWeek = await _statutory.ValueAsync(StatutoryKeys.StandardHoursPerWeek, DateTime.Today, 44m);

            return View(await _db.Shifts.AsNoTracking().OrderBy(s => s.Name).ToListAsync());
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> SaveShift(Shift input, int[]? workingDays)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A shift needs a name.";
                return RedirectToAction(nameof(Shifts));
            }

            // Rebuild the mask from the tick-boxes rather than trusting a posted integer.
            input.WorkingDaysMask = workingDays?.Aggregate(0, (mask, day) => mask | (1 << day)) ?? 0;

            if (input.WorkingDaysMask == 0)
            {
                TempData["Error"] = "Choose at least one working day.";
                return RedirectToAction(nameof(Shifts));
            }

            if (input.Id == 0) _db.Shifts.Add(input);
            else
            {
                var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == input.Id);
                if (shift == null) return NotFound();

                shift.Name = input.Name;
                shift.Description = input.Description;
                shift.StartTime = input.StartTime;
                shift.EndTime = input.EndTime;
                shift.BreakMinutes = input.BreakMinutes;
                shift.WorkingDaysMask = input.WorkingDaysMask;
                shift.LateGraceMinutes = input.LateGraceMinutes;
                shift.OvertimeThresholdMinutes = input.OvertimeThresholdMinutes;
                shift.SpansMidnight = input.SpansMidnight;
                shift.Colour = input.Colour;
                shift.IsActive = input.IsActive;
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync(input.Id == 0 ? "Created" : "Updated", nameof(Shift), input.Id,
                $"{input.Name} — {input.StartTime:hh\\:mm} to {input.EndTime:hh\\:mm}, "
                + $"{input.ScheduledHours:0.##}h/day on {input.WorkingDaysLabel}");

            TempData["Success"] = "Shift saved.";
            return RedirectToAction(nameof(Shifts));
        }

        [HttpPost]
        [RoleAuthorize("Admin", "SystemsAdmin", "HR")]
        public async Task<IActionResult> AssignShift(int employeeId, int shiftId, DateTime fromDate, string? note)
        {
            // A new assignment closes the standing one rather than leaving two open.
            var open = await _db.ShiftAssignments
                .Where(a => a.EmployeeId == employeeId && a.ToDate == null && a.FromDate < fromDate)
                .ToListAsync();
            foreach (var assignment in open) assignment.ToDate = fromDate.AddDays(-1);

            _db.ShiftAssignments.Add(new ShiftAssignment
            {
                EmployeeId = employeeId,
                ShiftId = shiftId,
                FromDate = fromDate.Date,
                Note = note
            });

            await _db.SaveChangesAsync();

            await _audit.LogAsync("ShiftAssigned", nameof(ShiftAssignment), employeeId,
                $"Employee #{employeeId} assigned shift #{shiftId} from {fromDate:d MMM yyyy}");

            TempData["Success"] = "Shift assigned.";
            return RedirectToAction(nameof(Shifts));
        }
    }
}
