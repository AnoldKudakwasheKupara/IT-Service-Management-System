using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// A category of leave and the rules that govern it. Configurable rather than hard-coded,
    /// because the Labour Act sets minimums that a contract or NEC agreement may improve on, and
    /// employers routinely add categories of their own (study, compassionate, unpaid sabbatical).
    /// </summary>
    public class LeaveType
    {
        public int Id { get; set; }

        [Required, StringLength(80)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Stable code the engine keys off — Vacation, Sick, Maternity, and so on.</summary>
        [Required, StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [StringLength(600)]
        public string? Description { get; set; }

        /// <summary>The statute or agreement this entitlement comes from, shown to the employee.</summary>
        [StringLength(200)]
        public string? Authority { get; set; }

        /// <summary>Days granted per leave cycle. Zero where the type accrues instead.</summary>
        [Column(TypeName = "decimal(9,2)")]
        [Display(Name = "Annual entitlement (days)")]
        public decimal AnnualEntitlementDays { get; set; }

        /// <summary>
        /// Days earned per completed month of service. Vacation leave accrues this way under
        /// s.14A; most other types are granted as a whole allowance instead.
        /// </summary>
        [Column(TypeName = "decimal(9,4)")]
        [Display(Name = "Accrues per month")]
        public decimal AccrualPerMonth { get; set; }

        public bool IsPaid { get; set; } = true;

        /// <summary>
        /// True for sick leave, which runs at full pay then half pay then unpaid rather than at a
        /// single rate — Labour Act s.14.
        /// </summary>
        public bool HasHalfPayTier { get; set; }

        [Display(Name = "Half-pay days")]
        public int HalfPayDays { get; set; }

        /// <summary>Unused days that may be carried into the next cycle. Zero means use-it-or-lose-it.</summary>
        [Column(TypeName = "decimal(9,2)")]
        [Display(Name = "Maximum carry-over (days)")]
        public decimal MaxCarryOverDays { get; set; }

        /// <summary>Months of service before the type can be taken at all — maternity requires this.</summary>
        [Display(Name = "Qualifying service (months)")]
        public int QualifyingMonths { get; set; }

        /// <summary>Whether a medical certificate must be attached. Sick leave requires one.</summary>
        [Display(Name = "Requires a medical certificate")]
        public bool RequiresMedicalCertificate { get; set; }

        /// <summary>Days of absence before the certificate becomes mandatory.</summary>
        public int CertificateRequiredAfterDays { get; set; } = 1;

        /// <summary>Restricts the type where the entitlement is sex-specific, as maternity is.</summary>
        [StringLength(20)]
        public string? RestrictedToGender { get; set; }

        /// <summary>
        /// Counted in working days (weekends and public holidays excluded) rather than calendar
        /// days. Vacation is normally working days; maternity is normally calendar days.
        /// </summary>
        [Display(Name = "Counted in working days")]
        public bool CountsWorkingDaysOnly { get; set; } = true;

        /// <summary>Notice the employee should give before taking it. Advisory, not enforced.</summary>
        public int NoticeDaysRequired { get; set; }

        /// <summary>Whether an unused balance is paid out when employment ends.</summary>
        [Display(Name = "Paid out on termination")]
        public bool PaidOutOnTermination { get; set; }

        /// <summary>Colour used on the leave calendar.</summary>
        [StringLength(20)]
        public string? Colour { get; set; }

        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }

        [ValidateNever] public ICollection<LeaveRequest> Requests { get; set; } = new List<LeaveRequest>();
    }

    /// <summary>
    /// An employee's balance for one leave type in one cycle. Kept as a stored row rather than
    /// recomputed on demand so a balance can be adjusted by hand — an opening balance on migration,
    /// a goodwill grant, a correction — and so history does not move when the rules change.
    /// </summary>
    public class LeaveBalance
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        public int LeaveTypeId { get; set; }
        [ValidateNever] public LeaveType? LeaveType { get; set; }

        /// <summary>The leave cycle this balance belongs to, normally the calendar year.</summary>
        public int CycleYear { get; set; } = DateTime.Today.Year;

        /// <summary>Days carried in from the previous cycle.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal OpeningBalance { get; set; }

        /// <summary>Days earned this cycle, whether accrued monthly or granted up front.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal Accrued { get; set; }

        /// <summary>Days approved and taken.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal Taken { get; set; }

        /// <summary>Days on approved future leave — committed but not yet taken.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal Booked { get; set; }

        /// <summary>Days requested and awaiting a decision. Held back so a balance cannot be double-spent.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal Pending { get; set; }

        /// <summary>Manual correction, positive or negative, with the reason recorded.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal Adjustment { get; set; }

        [StringLength(500)]
        public string? AdjustmentReason { get; set; }

        /// <summary>Days at half pay taken this cycle — sick leave only.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal HalfPayTaken { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>What the employee can still book, after everything already committed.</summary>
        [NotMapped]
        public decimal Available => OpeningBalance + Accrued + Adjustment - Taken - Booked - Pending;

        /// <summary>Total earned this cycle before anything was used.</summary>
        [NotMapped]
        public decimal Entitlement => OpeningBalance + Accrued + Adjustment;

        [NotMapped]
        public decimal Used => Taken + Booked;
    }

    /// <summary>A leave application and its journey through approval.</summary>
    public class LeaveRequest
    {
        public int Id { get; set; }

        [NotMapped]
        public string Reference => $"LV-{Id:D5}";

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        public int LeaveTypeId { get; set; }
        [ValidateNever] public LeaveType? LeaveType { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "First day")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Last day")]
        public DateTime EndDate { get; set; } = DateTime.Today;

        /// <summary>
        /// Days deducted from the balance. Computed at submission from the leave type's counting
        /// rule, then frozen — recomputing later would move history when the holiday calendar
        /// changes.
        /// </summary>
        [Column(TypeName = "decimal(9,2)")]
        [Display(Name = "Days")]
        public decimal Days { get; set; }

        /// <summary>Split for sick leave, which runs full pay then half pay then unpaid.</summary>
        [Column(TypeName = "decimal(9,2)")] public decimal FullPayDays { get; set; }
        [Column(TypeName = "decimal(9,2)")] public decimal HalfPayDays { get; set; }
        [Column(TypeName = "decimal(9,2)")] public decimal UnpaidDays { get; set; }

        /// <summary>Set when the employee takes a half day rather than a whole one.</summary>
        public bool IsHalfDay { get; set; }

        [StringLength(1000)]
        public string? Reason { get; set; }

        /// <summary>Who covers the work, and where the employee can be reached.</summary>
        public int? CoveringEmployeeId { get; set; }
        [ValidateNever] public Employee? CoveringEmployee { get; set; }

        [StringLength(120)]
        [Display(Name = "Contact while away")]
        public string? ContactWhileAway { get; set; }

        public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Draft;

        // ── Supporting document, e.g. a medical certificate ──────────────────────
        [StringLength(260)] public string? DocumentFileName { get; set; }
        [StringLength(500)] public string? DocumentPath { get; set; }

        // ── Approval ─────────────────────────────────────────────────────────────
        public int? ManagerApprovedById { get; set; }
        [ValidateNever] public Employee? ManagerApprovedBy { get; set; }
        public DateTime? ManagerApprovedAt { get; set; }

        public int? HrApprovedById { get; set; }
        [ValidateNever] public Employee? HrApprovedBy { get; set; }
        public DateTime? HrApprovedAt { get; set; }

        [StringLength(1000)]
        public string? DecisionNote { get; set; }

        public DateTime? CancelledAt { get; set; }
        [StringLength(500)] public string? CancellationReason { get; set; }

        public int SubmittedById { get; set; }
        [ValidateNever] public User? SubmittedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        public bool IsOpen => Status is LeaveRequestStatus.Draft or LeaveRequestStatus.Submitted
            or LeaveRequestStatus.ManagerApproved;

        [NotMapped]
        public bool IsCommitted => Status is LeaveRequestStatus.Approved or LeaveRequestStatus.Taken;

        [NotMapped]
        public bool IsInProgress => Status == LeaveRequestStatus.Approved
            && StartDate.Date <= DateTime.Today && EndDate.Date >= DateTime.Today;
    }

    /// <summary>
    /// Two-stage approval: the line manager first, then HR. HR is the second stage because only HR
    /// sees the balance across the whole organisation and the statutory position.
    /// </summary>
    public enum LeaveRequestStatus
    {
        Draft,
        Submitted,
        ManagerApproved,
        Approved,
        Rejected,
        Cancelled,
        Taken
    }

    /// <summary>
    /// A record of every movement on a balance. Leave is money — an unused vacation balance is
    /// paid out on termination — so the balance needs a ledger behind it rather than just a number
    /// that changes.
    /// </summary>
    public class LeaveLedgerEntry
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        public int LeaveTypeId { get; set; }
        [ValidateNever] public LeaveType? LeaveType { get; set; }

        public int? LeaveRequestId { get; set; }
        [ValidateNever] public LeaveRequest? LeaveRequest { get; set; }

        public int CycleYear { get; set; }

        public LeaveLedgerKind Kind { get; set; }

        /// <summary>Positive adds to the balance, negative takes from it.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal Days { get; set; }

        /// <summary>The balance after this movement, so the ledger can be read without re-adding it.</summary>
        [Column(TypeName = "decimal(9,2)")]
        public decimal BalanceAfter { get; set; }

        [StringLength(400)]
        public string? Narrative { get; set; }

        public int? RecordedById { get; set; }
        [ValidateNever] public User? RecordedBy { get; set; }

        public DateTime At { get; set; } = DateTime.Now;
    }

    public enum LeaveLedgerKind
    {
        OpeningBalance,
        Accrual,
        Taken,
        Cancelled,
        Adjustment,
        CarriedOver,
        Forfeited,
        PaidOut
    }
}
