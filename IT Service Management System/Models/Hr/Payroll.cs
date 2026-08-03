using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// An employee's standing pay arrangement — what they earn and in which currency, before any
    /// of the month's variable items are applied.
    /// </summary>
    public class SalaryStructure
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        /// <summary>Basic pay for the period. Everything statutory is computed from gross, not this.</summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Basic salary")]
        public decimal BasicSalary { get; set; }

        [Required, StringLength(10)]
        public string Currency { get; set; } = "USD";

        public PayPeriod Period { get; set; } = PayPeriod.Monthly;

        /// <summary>
        /// Effective-dated rather than overwritten, so a back-dated increase does not silently
        /// restate every payslip already issued.
        /// </summary>
        [DataType(DataType.Date)]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime? EffectiveTo { get; set; }

        [StringLength(300)]
        public string? Reason { get; set; }

        /// <summary>Where a pension or medical aid contribution is a fixed sum rather than a rate.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PensionContribution { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MedicalAidContribution { get; set; }

        /// <summary>Bank details for the payment run, where they differ from the employee record.</summary>
        [StringLength(120)] public string? BankName { get; set; }
        [StringLength(60)] public string? BankAccountNumber { get; set; }

        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public bool IsCurrent => EffectiveFrom <= DateTime.Today && (EffectiveTo == null || EffectiveTo >= DateTime.Today);
    }

    /// <summary>
    /// A recurring or one-off addition to, or deduction from, pay. Kept as its own record so a
    /// payslip can show every line rather than a single net figure nobody can question.
    /// </summary>
    public class PayComponent
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        public PayComponentType Type { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>When set, the amount is a percentage of basic pay rather than a fixed sum.</summary>
        [Column(TypeName = "decimal(9,4)")]
        public decimal? PercentageOfBasic { get; set; }

        /// <summary>
        /// Whether the component is subject to PAYE. Some allowances are exempt or partially
        /// exempt under the Income Tax Act; a reimbursement of actual expense is not income at all.
        /// </summary>
        [Display(Name = "Subject to PAYE")]
        public bool IsTaxable { get; set; } = true;

        /// <summary>Whether it counts towards NSSA insurable earnings.</summary>
        [Display(Name = "Counts towards NSSA")]
        public bool IsPensionable { get; set; } = true;

        /// <summary>Recurring components apply every period until the end date; one-offs apply once.</summary>
        public bool IsRecurring { get; set; } = true;

        [DataType(DataType.Date)] public DateTime EffectiveFrom { get; set; } = DateTime.Today;
        [DataType(DataType.Date)] public DateTime? EffectiveTo { get; set; }

        [StringLength(400)] public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public enum PayComponentType
    {
        /// <summary>Housing, transport, and the like.</summary>
        Allowance,
        /// <summary>Overtime, commission, a bonus.</summary>
        Earning,
        /// <summary>A reimbursement of actual expense — not income, so never taxable.</summary>
        Reimbursement,
        /// <summary>A voluntary deduction: union dues, a savings scheme.</summary>
        Deduction,
        /// <summary>Recovery of a loan or salary advance.</summary>
        LoanRepayment,
        /// <summary>An order of court or a statutory garnishee.</summary>
        Garnishee
    }

    /// <summary>
    /// One month's payroll for the whole organisation. A run is a unit of work that is prepared,
    /// checked, approved and only then paid — and once paid it is locked, because a payslip that
    /// can change after issue is worthless as evidence.
    /// </summary>
    public class PayrollRun
    {
        public int Id { get; set; }

        [NotMapped]
        public string Reference => $"PR-{PeriodYear}-{PeriodMonth:D2}";

        [Display(Name = "Year")]
        public int PeriodYear { get; set; } = DateTime.Today.Year;

        [Display(Name = "Month")]
        public int PeriodMonth { get; set; } = DateTime.Today.Month;

        [Required, StringLength(10)]
        public string Currency { get; set; } = "USD";

        [DataType(DataType.Date)]
        [Display(Name = "Pay date")]
        public DateTime PayDate { get; set; } = DateTime.Today;

        /// <summary>
        /// The date the statutory tables are read at. Normally the last day of the period, so a
        /// rerun months later still applies the rates that were in force then.
        /// </summary>
        [DataType(DataType.Date)]
        public DateTime StatutoryAsAt { get; set; } = DateTime.Today;

        public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

        // ── Totals, held so a summary does not have to re-sum every payslip ──────
        [Column(TypeName = "decimal(18,2)")] public decimal TotalGross { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalPaye { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalAidsLevy { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalNssaEmployee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalNssaEmployer { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalOtherDeductions { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalNet { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalZimdef { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalStandardsLevy { get; set; }

        public int EmployeeCount { get; set; }

        public int? PreparedById { get; set; }
        [ValidateNever] public User? PreparedBy { get; set; }
        public DateTime? PreparedAt { get; set; }

        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public DateTime? PaidAt { get; set; }

        [StringLength(1000)] public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<Payslip> Payslips { get; set; } = new List<Payslip>();

        /// <summary>Total cost to the employer — gross plus the contributions and levies it bears.</summary>
        [NotMapped]
        public decimal TotalEmployerCost => TotalGross + TotalNssaEmployer + TotalZimdef + TotalStandardsLevy;

        /// <summary>A locked run can no longer be recalculated.</summary>
        [NotMapped]
        public bool IsLocked => Status is PayrollRunStatus.Approved or PayrollRunStatus.Paid;

        [NotMapped]
        public string PeriodName => new DateTime(PeriodYear, PeriodMonth, 1).ToString("MMMM yyyy");
    }

    public enum PayrollRunStatus { Draft, Calculated, Approved, Paid, Cancelled }

    /// <summary>
    /// One employee's pay for one run. Every statutory figure is stored rather than recomputed on
    /// display, so a payslip reprinted in three years shows what was actually paid.
    /// </summary>
    public class Payslip
    {
        public int Id { get; set; }

        public int PayrollRunId { get; set; }
        [ValidateNever] public PayrollRun? PayrollRun { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        [Required, StringLength(10)]
        public string Currency { get; set; } = "USD";

        // ── Earnings ─────────────────────────────────────────────────────────────
        [Column(TypeName = "decimal(18,2)")] public decimal BasicSalary { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal Allowances { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal Overtime { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal OtherEarnings { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal Reimbursements { get; set; }

        /// <summary>Everything that counts as income, before deductions.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal Gross { get; set; }

        /// <summary>Gross less the deductions allowed against it — the base PAYE is computed on.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal TaxableIncome { get; set; }

        // ── Statutory deductions ─────────────────────────────────────────────────
        [Column(TypeName = "decimal(18,2)")] public decimal Paye { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal AidsLevy { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal NssaEmployee { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal NssaInsurableEarnings { get; set; }

        // ── Other deductions ─────────────────────────────────────────────────────
        [Column(TypeName = "decimal(18,2)")] public decimal PensionContribution { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal MedicalAid { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal LoanRepayments { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal OtherDeductions { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal TotalDeductions { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal Net { get; set; }

        // ── Employer-borne costs, shown for the cost-to-company view ─────────────
        [Column(TypeName = "decimal(18,2)")] public decimal NssaEmployer { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal NssaAccidentPrevention { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal Zimdef { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal StandardsLevy { get; set; }

        // ── Unpaid absence, which reduces pay ────────────────────────────────────
        [Column(TypeName = "decimal(9,2)")] public decimal UnpaidLeaveDays { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal UnpaidLeaveDeduction { get; set; }
        [Column(TypeName = "decimal(9,2)")] public decimal HalfPayLeaveDays { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal HalfPayLeaveDeduction { get; set; }

        /// <summary>
        /// The marginal PAYE rate applied, kept so an employee querying their tax can be shown the
        /// band without the table having to be reconstructed.
        /// </summary>
        [Column(TypeName = "decimal(9,4)")] public decimal MarginalTaxRate { get; set; }

        [StringLength(2000)] public string? Notes { get; set; }

        public DateTime CalculatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<PayslipLine> Lines { get; set; } = new List<PayslipLine>();

        [NotMapped]
        public decimal EmployerCost => Gross + NssaEmployer + NssaAccidentPrevention + Zimdef + StandardsLevy;
    }

    /// <summary>
    /// A single line on a payslip. The totals on <see cref="Payslip"/> are what gets reported; the
    /// lines are what makes them explicable to the person being paid.
    /// </summary>
    public class PayslipLine
    {
        public int Id { get; set; }

        public int PayslipId { get; set; }
        [ValidateNever] public Payslip? Payslip { get; set; }

        [Required, StringLength(150)]
        public string Description { get; set; } = string.Empty;

        public PayslipLineKind Kind { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>The Act or agreement behind a statutory line, shown on the payslip itself.</summary>
        [StringLength(200)]
        public string? Basis { get; set; }

        public int DisplayOrder { get; set; }
    }

    public enum PayslipLineKind { Earning, StatutoryDeduction, Deduction, EmployerContribution, Information }
}
