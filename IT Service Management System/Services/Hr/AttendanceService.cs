using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Attendance: clocking, the daily reconciliation that turns a missing record into a recorded
    /// absence, and the overtime split that payroll pays on.
    /// <para>
    /// Overtime is separated by the rate it attracts rather than lumped together, because the three
    /// are paid differently — beyond the shift on a normal day, worked on a rest day, and worked on
    /// a public holiday. The multipliers come from the statutory store, since they are set by the
    /// National Employment Council agreement for the sector rather than by the Labour Act.
    /// </para>
    /// </summary>
    public class AttendanceService
    {
        private readonly ApplicationDbContext _db;
        private readonly StatutoryService _statutory;
        private readonly ILogger<AttendanceService> _log;

        public AttendanceService(ApplicationDbContext db, StatutoryService statutory, ILogger<AttendanceService> log)
        {
            _db = db; _statutory = statutory; _log = log;
        }

        // ── Clocking ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Clock an employee in. Returns the existing record if they are already clocked in, rather
        /// than creating a second one for the same day.
        /// </summary>
        public async Task<ClockResult> ClockInAsync(int employeeId, DateTime at, int? recordedByUserId = null)
        {
            var date = at.Date;

            var existing = await _db.AttendanceRecords
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == date);

            if (existing?.ClockIn != null)
                return new ClockResult(false, existing,
                    existing.ClockOut == null
                        ? $"Already clocked in at {existing.ClockIn:HH:mm}."
                        : $"Already worked today — {existing.ClockIn:HH:mm} to {existing.ClockOut:HH:mm}.");

            var shift = await ShiftForAsync(employeeId, date);
            var dayType = await ClassifyDayAsync(date, shift);

            var record = existing ?? new AttendanceRecord { EmployeeId = employeeId, Date = date };

            record.ShiftId = shift?.Id;
            record.ClockIn = at;
            record.BreakMinutes = shift?.BreakMinutes ?? 0;
            record.DayType = dayType;
            record.ScheduledHours = dayType == DayType.WorkingDay ? shift?.ScheduledHours ?? 0 : 0;
            record.RecordedById = recordedByUserId;
            record.IsManualEntry = false;
            record.UpdatedAt = DateTime.Now;

            // Lateness only means anything against a shift on a normal working day.
            if (shift != null && dayType == DayType.WorkingDay)
            {
                var due = date.Add(shift.StartTime);
                var late = (int)(at - due).TotalMinutes;
                record.LateMinutes = late > shift.LateGraceMinutes ? late : 0;
                record.Status = record.LateMinutes > 0 ? AttendanceStatus.Late : AttendanceStatus.Present;
            }
            else
            {
                record.Status = AttendanceStatus.Present;
            }

            if (existing == null) _db.AttendanceRecords.Add(record);
            await _db.SaveChangesAsync();

            var message = record.LateMinutes > 0
                ? $"Clocked in at {at:HH:mm} — {record.LateMinutes} minute(s) late."
                : $"Clocked in at {at:HH:mm}.";

            if (dayType != DayType.WorkingDay)
                message += dayType == DayType.PublicHoliday
                    ? " Today is a public holiday, so the hours attract the holiday rate."
                    : " Today is a rest day, so the hours attract the rest-day rate.";

            return new ClockResult(true, record, message);
        }

        /// <summary>Clock out and compute the day, splitting overtime by the rate it attracts.</summary>
        public async Task<ClockResult> ClockOutAsync(int employeeId, DateTime at, int? recordedByUserId = null)
        {
            // A night shift is clocked out on the following calendar day, so look back one day too.
            var record = await _db.AttendanceRecords
                .Include(a => a.Shift)
                .Where(a => a.EmployeeId == employeeId
                         && a.ClockIn != null && a.ClockOut == null
                         && a.Date >= at.Date.AddDays(-1) && a.Date <= at.Date)
                .OrderByDescending(a => a.Date)
                .FirstOrDefaultAsync();

            if (record == null)
                return new ClockResult(false, null, "You are not clocked in.");

            if (at <= record.ClockIn)
                return new ClockResult(false, record, "Clock-out cannot be before clock-in.");

            record.ClockOut = at;
            record.RecordedById ??= recordedByUserId;
            Recalculate(record);
            record.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            var message = $"Clocked out at {at:HH:mm} — {record.HoursWorked:0.##} hour(s) worked.";
            if (record.TotalOvertimeHours > 0)
                message += $" {record.TotalOvertimeHours:0.##} hour(s) of overtime, awaiting approval before payroll picks it up.";

            return new ClockResult(true, record, message);
        }

        public record ClockResult(bool Succeeded, AttendanceRecord? Record, string Message);

        // ── Calculation ──────────────────────────────────────────────────────────

        /// <summary>
        /// Work out hours and the overtime split from the clock times. Kept separate from clocking
        /// so a corrected record recomputes exactly the same way a clocked one did.
        /// </summary>
        public static void Recalculate(AttendanceRecord record)
        {
            if (record.ClockIn == null || record.ClockOut == null)
            {
                record.HoursWorked = 0;
                record.OvertimeHours = record.RestDayHours = record.PublicHolidayHours = 0;
                return;
            }

            var minutes = (record.ClockOut.Value - record.ClockIn.Value).TotalMinutes - record.BreakMinutes;
            record.HoursWorked = Math.Round((decimal)Math.Max(0, minutes) / 60m, 2);

            record.OvertimeHours = record.RestDayHours = record.PublicHolidayHours = 0;

            switch (record.DayType)
            {
                // Every hour on a rest day or public holiday is at the premium rate — there is no
                // ordinary time on a day the employee was not scheduled to work.
                case DayType.PublicHoliday:
                    record.PublicHolidayHours = record.HoursWorked;
                    record.Status = AttendanceStatus.Present;
                    break;

                case DayType.RestDay:
                    record.RestDayHours = record.HoursWorked;
                    record.Status = AttendanceStatus.Present;
                    break;

                default:
                    var threshold = (decimal)(record.Shift?.OvertimeThresholdMinutes ?? 0) / 60m;
                    var beyond = record.HoursWorked - record.ScheduledHours;

                    if (beyond > threshold) record.OvertimeHours = Math.Round(beyond, 2);

                    if (record.Shift != null && record.ScheduledHours > 0)
                    {
                        var shortfall = record.ScheduledHours - record.HoursWorked;
                        record.EarlyLeaveMinutes = shortfall > 0 ? (int)Math.Round(shortfall * 60m) : 0;

                        // A meaningful shortfall is a partial day, not a full one — half an hour is
                        // noise, half a shift is not.
                        if (shortfall > 0.5m && record.Status != AttendanceStatus.OnLeave)
                            record.Status = AttendanceStatus.PartialDay;
                        else if (record.LateMinutes > 0)
                            record.Status = AttendanceStatus.Late;
                        else
                            record.Status = AttendanceStatus.Present;
                    }
                    break;
            }
        }

        /// <summary>Is this a working day, a rest day or a public holiday for this employee?</summary>
        private async Task<DayType> ClassifyDayAsync(DateTime date, Shift? shift)
        {
            var holidays = await _statutory.PublicHolidaysAsync(date, date);
            if (holidays.Contains(date.Date)) return DayType.PublicHoliday;

            // With no shift assigned, fall back to the ordinary Monday-to-Friday assumption.
            if (shift == null)
                return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                    ? DayType.RestDay : DayType.WorkingDay;

            return shift.WorksOn(date.DayOfWeek) ? DayType.WorkingDay : DayType.RestDay;
        }

        /// <summary>The shift an employee was on for a given date.</summary>
        public async Task<Shift?> ShiftForAsync(int employeeId, DateTime date) =>
            await _db.ShiftAssignments.AsNoTracking()
                .Where(a => a.EmployeeId == employeeId
                         && a.FromDate <= date
                         && (a.ToDate == null || a.ToDate >= date))
                .OrderByDescending(a => a.FromDate)
                .Select(a => a.Shift)
                .FirstOrDefaultAsync();

        // ── Daily reconciliation ─────────────────────────────────────────────────

        /// <summary>
        /// Create the missing records for a date: anybody who did not clock gets a row saying why —
        /// on leave, rest day, public holiday, or absent.
        /// <para>
        /// An absence has to be a record rather than the absence of one. Otherwise it cannot be
        /// queried, cannot be explained, and quietly becomes a gap nobody notices until payroll.
        /// </para>
        /// </summary>
        public async Task<int> ReconcileDayAsync(DateTime date, CancellationToken ct = default)
        {
            date = date.Date;
            if (date > DateTime.Today) return 0;

            var employees = await _db.Employees.AsNoTracking()
                .Where(e => e.Status == EmploymentStatus.Active
                         || e.Status == EmploymentStatus.OnProbation
                         || e.Status == EmploymentStatus.OnLeave)
                .Select(e => e.Id)
                .ToListAsync(ct);
            if (employees.Count == 0) return 0;

            var already = await _db.AttendanceRecords
                .Where(a => a.Date == date && employees.Contains(a.EmployeeId))
                .Select(a => a.EmployeeId)
                .ToListAsync(ct);

            var missing = employees.Except(already).ToList();
            if (missing.Count == 0) return 0;

            var isHoliday = (await _statutory.PublicHolidaysAsync(date, date)).Contains(date);

            // Approved leave covering the date, so a day off is never recorded as an absence.
            var onLeave = await _db.LeaveRequests.AsNoTracking()
                .Where(r => missing.Contains(r.EmployeeId)
                         && (r.Status == LeaveRequestStatus.Approved || r.Status == LeaveRequestStatus.Taken)
                         && r.StartDate <= date && r.EndDate >= date)
                .Select(r => new { r.EmployeeId, r.Id })
                .ToListAsync(ct);
            var leaveByEmployee = onLeave.GroupBy(l => l.EmployeeId)
                .ToDictionary(g => g.Key, g => g.First().Id);

            var assignments = await _db.ShiftAssignments.AsNoTracking()
                .Include(a => a.Shift)
                .Where(a => missing.Contains(a.EmployeeId)
                         && a.FromDate <= date && (a.ToDate == null || a.ToDate >= date))
                .ToListAsync(ct);
            var shiftByEmployee = assignments
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FromDate).First().Shift);

            foreach (var employeeId in missing)
            {
                shiftByEmployee.TryGetValue(employeeId, out var shift);

                var worksToday = shift?.WorksOn(date.DayOfWeek)
                    ?? date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

                var record = new AttendanceRecord
                {
                    EmployeeId = employeeId,
                    ShiftId = shift?.Id,
                    Date = date,
                    DayType = isHoliday ? DayType.PublicHoliday
                            : worksToday ? DayType.WorkingDay : DayType.RestDay,
                    ScheduledHours = !isHoliday && worksToday ? shift?.ScheduledHours ?? 0 : 0,
                    // Nothing was clocked, so nothing is disputed — these are approved on creation.
                    IsApproved = true,
                    CreatedAt = DateTime.Now
                };

                if (leaveByEmployee.TryGetValue(employeeId, out var leaveId))
                {
                    record.Status = AttendanceStatus.OnLeave;
                    record.LeaveRequestId = leaveId;
                }
                else if (isHoliday) record.Status = AttendanceStatus.Holiday;
                else if (!worksToday) record.Status = AttendanceStatus.RestDay;
                else
                {
                    // A working day with no clocking and no leave needs explaining, so it is
                    // flagged Unexplained rather than settled as Absent.
                    record.Status = AttendanceStatus.Unexplained;
                    record.IsApproved = false;
                }

                _db.AttendanceRecords.Add(record);
            }

            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Attendance reconciled for {Date:d}: {Count} record(s) created.", date, missing.Count);
            return missing.Count;
        }

        /// <summary>Close out anyone who clocked in on a past day and never clocked out.</summary>
        public async Task<int> FlagIncompleteAsync(CancellationToken ct = default)
        {
            var stale = await _db.AttendanceRecords
                .Where(a => a.ClockIn != null && a.ClockOut == null && a.Date < DateTime.Today)
                .ToListAsync(ct);

            foreach (var record in stale)
            {
                record.Notes = string.IsNullOrWhiteSpace(record.Notes)
                    ? "No clock-out recorded. A supervisor must enter the finishing time before this "
                    + "day can be approved."
                    : record.Notes;
                record.IsApproved = false;
            }

            if (stale.Count > 0) await _db.SaveChangesAsync(ct);
            return stale.Count;
        }

        // ── Overtime for payroll ─────────────────────────────────────────────────

        /// <summary>
        /// Approved overtime for a period, valued at the multipliers in force then. Payroll uses
        /// this to add an overtime component rather than recomputing the rating itself.
        /// </summary>
        public async Task<List<OvertimeValuation>> ValueOvertimeAsync(
            DateTime periodStart, DateTime periodEnd, DateTime asAt)
        {
            var multipliers = await _statutory.ValuesAsync(new[]
            {
                StatutoryKeys.OvertimeMultiplier,
                StatutoryKeys.RestDayMultiplier,
                StatutoryKeys.PublicHolidayMultiplier,
                StatutoryKeys.StandardHoursPerWeek
            }, asAt);

            var overtimeRate = multipliers.GetValueOrDefault(StatutoryKeys.OvertimeMultiplier, 1.5m);
            var restDayRate = multipliers.GetValueOrDefault(StatutoryKeys.RestDayMultiplier, 2m);
            var holidayRate = multipliers.GetValueOrDefault(StatutoryKeys.PublicHolidayMultiplier, 2m);
            var weeklyHours = multipliers.GetValueOrDefault(StatutoryKeys.StandardHoursPerWeek, 44m);

            var records = await _db.AttendanceRecords.AsNoTracking()
                .Where(a => a.Date >= periodStart && a.Date <= periodEnd
                         && a.IsApproved
                         && (a.OvertimeHours > 0 || a.RestDayHours > 0 || a.PublicHolidayHours > 0))
                .GroupBy(a => a.EmployeeId)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    Overtime = g.Sum(a => a.OvertimeHours),
                    RestDay = g.Sum(a => a.RestDayHours),
                    Holiday = g.Sum(a => a.PublicHolidayHours)
                })
                .ToListAsync();
            if (records.Count == 0) return new List<OvertimeValuation>();

            var employeeIds = records.Select(r => r.EmployeeId).ToList();

            var salaries = await _db.SalaryStructures.AsNoTracking()
                .Where(s => employeeIds.Contains(s.EmployeeId)
                         && s.EffectiveFrom <= periodEnd
                         && (s.EffectiveTo == null || s.EffectiveTo >= periodStart))
                .ToListAsync();

            var currentSalary = salaries
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EffectiveFrom).First());

            var results = new List<OvertimeValuation>();

            foreach (var row in records)
            {
                if (!currentSalary.TryGetValue(row.EmployeeId, out var salary)) continue;

                // Hourly rate from the monthly salary: weekly hours × 52 ÷ 12 gives monthly hours.
                var monthlyHours = weeklyHours * 52m / 12m;
                var hourly = monthlyHours > 0 ? salary.BasicSalary / monthlyHours : 0m;

                results.Add(new OvertimeValuation(
                    row.EmployeeId,
                    salary.Currency,
                    Math.Round(hourly, 4),
                    row.Overtime, Math.Round(row.Overtime * hourly * overtimeRate, 2), overtimeRate,
                    row.RestDay, Math.Round(row.RestDay * hourly * restDayRate, 2), restDayRate,
                    row.Holiday, Math.Round(row.Holiday * hourly * holidayRate, 2), holidayRate));
            }

            return results;
        }

        public record OvertimeValuation(
            int EmployeeId, string Currency, decimal HourlyRate,
            decimal OvertimeHours, decimal OvertimeAmount, decimal OvertimeMultiplier,
            decimal RestDayHours, decimal RestDayAmount, decimal RestDayMultiplier,
            decimal HolidayHours, decimal HolidayAmount, decimal HolidayMultiplier)
        {
            public decimal TotalHours => OvertimeHours + RestDayHours + HolidayHours;
            public decimal TotalAmount => OvertimeAmount + RestDayAmount + HolidayAmount;
        }

        // ── Summaries ────────────────────────────────────────────────────────────

        /// <summary>One employee's attendance over a period, for the timesheet view.</summary>
        public async Task<AttendanceSummary> SummariseAsync(int employeeId, DateTime from, DateTime to)
        {
            var records = await _db.AttendanceRecords.AsNoTracking()
                .Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date <= to)
                .ToListAsync();

            return new AttendanceSummary
            {
                From = from,
                To = to,
                DaysWorked = records.Count(r => r.HoursWorked > 0),
                DaysAbsent = records.Count(r => r.Status is AttendanceStatus.Absent or AttendanceStatus.Unexplained),
                DaysOnLeave = records.Count(r => r.Status == AttendanceStatus.OnLeave),
                TimesLate = records.Count(r => r.LateMinutes > 0),
                TotalLateMinutes = records.Sum(r => r.LateMinutes),
                HoursWorked = records.Sum(r => r.HoursWorked),
                ScheduledHours = records.Sum(r => r.ScheduledHours),
                OvertimeHours = records.Sum(r => r.OvertimeHours),
                RestDayHours = records.Sum(r => r.RestDayHours),
                PublicHolidayHours = records.Sum(r => r.PublicHolidayHours),
                UnapprovedOvertime = records.Count(r => r.TotalOvertimeHours > 0 && !r.IsApproved),
                IncompleteDays = records.Count(r => r.IsIncomplete)
            };
        }

        public class AttendanceSummary
        {
            public DateTime From { get; set; }
            public DateTime To { get; set; }
            public int DaysWorked { get; set; }
            public int DaysAbsent { get; set; }
            public int DaysOnLeave { get; set; }
            public int TimesLate { get; set; }
            public int TotalLateMinutes { get; set; }
            public decimal HoursWorked { get; set; }
            public decimal ScheduledHours { get; set; }
            public decimal OvertimeHours { get; set; }
            public decimal RestDayHours { get; set; }
            public decimal PublicHolidayHours { get; set; }
            public int UnapprovedOvertime { get; set; }
            public int IncompleteDays { get; set; }

            public decimal TotalOvertimeHours => OvertimeHours + RestDayHours + PublicHolidayHours;

            /// <summary>Hours worked against hours scheduled. Over 100% means overtime was worked.</summary>
            public int AttendancePercent =>
                ScheduledHours <= 0 ? 0 : (int)Math.Round(HoursWorked / ScheduledHours * 100);
        }
    }
}
