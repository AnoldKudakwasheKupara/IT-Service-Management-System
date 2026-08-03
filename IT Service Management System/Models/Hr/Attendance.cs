using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// A working pattern — when the day starts and ends, how long the break is, and which days of
    /// the week it covers.
    /// <para>
    /// The Labour Act does not fix a universal working week; hours are set by the National
    /// Employment Council agreement for the sector. A shift therefore carries its own hours rather
    /// than inheriting a national figure, so an employer bound by a 40-hour agreement and one bound
    /// by 48 can both be represented.
    /// </para>
    /// </summary>
    public class Shift
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Starts at")]
        public TimeSpan StartTime { get; set; } = new(8, 0, 0);

        [Display(Name = "Ends at")]
        public TimeSpan EndTime { get; set; } = new(17, 0, 0);

        /// <summary>Unpaid break within the shift, deducted from hours worked.</summary>
        [Display(Name = "Break (minutes)")]
        public int BreakMinutes { get; set; } = 60;

        /// <summary>
        /// Days the shift runs, as a bitmask of <see cref="DayOfWeek"/> — bit 0 is Sunday.
        /// A mask rather than seven columns, so an unusual pattern needs no schema change.
        /// </summary>
        [Display(Name = "Working days")]
        public int WorkingDaysMask { get; set; } = 0b0111110;   // Monday to Friday

        /// <summary>
        /// Grace before an arrival counts as late. Most NEC agreements allow a few minutes, and
        /// flagging someone late for being ninety seconds behind is not a useful signal.
        /// </summary>
        [Display(Name = "Late after (minutes)")]
        public int LateGraceMinutes { get; set; } = 10;

        /// <summary>Minutes worked past the end of shift before overtime starts accruing.</summary>
        [Display(Name = "Overtime after (minutes)")]
        public int OvertimeThresholdMinutes { get; set; } = 30;

        /// <summary>A shift crossing midnight — nights are common in security and manufacturing.</summary>
        [Display(Name = "Crosses midnight")]
        public bool SpansMidnight { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(20)]
        public string? Colour { get; set; }

        /// <summary>Scheduled hours per working day, net of the break.</summary>
        [NotMapped]
        public decimal ScheduledHours
        {
            get
            {
                var span = SpansMidnight
                    ? EndTime.Add(TimeSpan.FromDays(1)) - StartTime
                    : EndTime - StartTime;
                return Math.Round((decimal)(span.TotalMinutes - BreakMinutes) / 60m, 2);
            }
        }

        /// <summary>True when the shift runs on that day of the week.</summary>
        public bool WorksOn(DayOfWeek day) => (WorkingDaysMask & (1 << (int)day)) != 0;

        [NotMapped]
        public string WorkingDaysLabel
        {
            get
            {
                var names = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
                var days = Enumerable.Range(0, 7).Where(d => (WorkingDaysMask & (1 << d)) != 0).Select(d => names[d]);
                return days.Any() ? string.Join(", ", days) : "none";
            }
        }

        [ValidateNever] public ICollection<ShiftAssignment> Assignments { get; set; } = new List<ShiftAssignment>();
    }

    /// <summary>Which shift an employee works, over which period.</summary>
    public class ShiftAssignment
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        public int ShiftId { get; set; }
        [ValidateNever] public Shift? Shift { get; set; }

        [DataType(DataType.Date)] public DateTime FromDate { get; set; } = DateTime.Today;

        /// <summary>Null while this is the employee's standing pattern.</summary>
        [DataType(DataType.Date)] public DateTime? ToDate { get; set; }

        [StringLength(300)] public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public bool IsCurrent => FromDate <= DateTime.Today && (ToDate == null || ToDate >= DateTime.Today);
    }

    /// <summary>
    /// One employee, one day. Created when they clock in, or by the daily reconciliation for a day
    /// nobody clocked — an absence has to be a record, not the absence of one, or it cannot be
    /// queried, explained or corrected.
    /// </summary>
    public class AttendanceRecord
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        public int? ShiftId { get; set; }
        [ValidateNever] public Shift? Shift { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        public DateTime? ClockIn { get; set; }
        public DateTime? ClockOut { get; set; }

        /// <summary>Unpaid break taken, in minutes. Defaults from the shift, adjustable.</summary>
        public int BreakMinutes { get; set; }

        /// <summary>Hours actually worked, net of break. Frozen once the day is approved.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal HoursWorked { get; set; }

        /// <summary>Hours the shift called for. Zero on a rest day or public holiday.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal ScheduledHours { get; set; }

        // ── Overtime, split by the rate it attracts ──────────────────────────────
        /// <summary>Beyond the shift on a normal working day.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal OvertimeHours { get; set; }

        /// <summary>Worked on a scheduled rest day.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal RestDayHours { get; set; }

        /// <summary>Worked on a public holiday.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal PublicHolidayHours { get; set; }

        public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

        public DayType DayType { get; set; } = DayType.WorkingDay;

        /// <summary>Minutes late against the shift start, after the grace period.</summary>
        public int LateMinutes { get; set; }

        /// <summary>Minutes short of the shift end.</summary>
        public int EarlyLeaveMinutes { get; set; }

        /// <summary>Set when the day is covered by approved leave, so it is not counted absent.</summary>
        public int? LeaveRequestId { get; set; }
        [ValidateNever] public LeaveRequest? LeaveRequest { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// True when a supervisor entered or corrected the times rather than the employee clocking.
        /// Kept because a manually-keyed attendance record that feeds overtime pay needs to be
        /// visibly distinguishable from a clocked one.
        /// </summary>
        [Display(Name = "Entered manually")]
        public bool IsManualEntry { get; set; }

        public int? RecordedById { get; set; }
        [ValidateNever] public User? RecordedBy { get; set; }

        // ── Approval — overtime is money, so it is signed off before payroll sees it ──
        public bool IsApproved { get; set; }
        public int? ApprovedById { get; set; }
        [ValidateNever] public Employee? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        public decimal TotalOvertimeHours => OvertimeHours + RestDayHours + PublicHolidayHours;

        [NotMapped]
        public bool IsIncomplete => ClockIn.HasValue && !ClockOut.HasValue;

        [NotMapped]
        public bool NeedsAttention =>
            IsIncomplete || (TotalOvertimeHours > 0 && !IsApproved) || Status == AttendanceStatus.Absent;
    }

    public enum AttendanceStatus
    {
        Present,
        Late,
        Absent,
        /// <summary>Covered by approved leave — not an absence.</summary>
        OnLeave,
        /// <summary>A scheduled rest day the employee did not work.</summary>
        RestDay,
        /// <summary>A public holiday the employee did not work.</summary>
        Holiday,
        /// <summary>Worked less than the shift, without approved leave for the balance.</summary>
        PartialDay,
        /// <summary>Absent, and the reason not yet established.</summary>
        Unexplained
    }

    /// <summary>What kind of day this was for the employee, which decides the overtime rate.</summary>
    public enum DayType { WorkingDay, RestDay, PublicHoliday }

    /// <summary>
    /// Overtime asked for in advance. Pre-approval matters because overtime is paid at a premium
    /// and, once worked, is very hard to refuse — so the decision belongs before the work, not
    /// after it.
    /// </summary>
    public class OvertimeRequest
    {
        public int Id { get; set; }

        [NotMapped]
        public string Reference => $"OT-{Id:D5}";

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Column(TypeName = "decimal(9,2)")]
        [Display(Name = "Hours requested")]
        public decimal HoursRequested { get; set; }

        [Column(TypeName = "decimal(9,2)")]
        [Display(Name = "Hours actually worked")]
        public decimal HoursApproved { get; set; }

        public DayType DayType { get; set; } = DayType.WorkingDay;

        [Required, StringLength(1000)]
        public string Reason { get; set; } = string.Empty;

        public OvertimeStatus Status { get; set; } = OvertimeStatus.Requested;

        public int? ApprovedById { get; set; }
        [ValidateNever] public Employee? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [StringLength(500)] public string? DecisionNote { get; set; }

        /// <summary>Set once the hours have been carried into a payroll run, so they cannot be paid twice.</summary>
        public int? PaidInPayrollRunId { get; set; }

        public int RequestedById { get; set; }
        [ValidateNever] public User? RequestedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public bool IsOpen => Status is OvertimeStatus.Requested;

        [NotMapped]
        public bool IsPayable => Status == OvertimeStatus.Approved && PaidInPayrollRunId == null;
    }

    public enum OvertimeStatus { Requested, Approved, Rejected, Cancelled, Paid }
}
