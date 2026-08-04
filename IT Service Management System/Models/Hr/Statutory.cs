using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// A statutory rate, ceiling or threshold, held as data with an effective-from date rather than
    /// as a constant in code.
    /// <para>
    /// Zimbabwe's payroll parameters move constantly — PAYE bands are reset by each Finance Act,
    /// the NSSA insurable-earnings ceiling is re-gazetted as the currency moves, and levies change
    /// with the national budget. Hard-coding any of them guarantees the system is wrong within
    /// months and silently recalculates history when it is corrected. Every value therefore carries
    /// the date it took effect and a citation, and lookups are always as-at a date.
    /// </para>
    /// </summary>
    public class StatutoryParameter
    {
        public int Id { get; set; }

        /// <summary>Stable lookup key, e.g. "NSSA.POBS.EmployeeRate". See <see cref="StatutoryKeys"/>.</summary>
        [Required, StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The value. A rate is stored as a percentage (4.5 means 4.5%), an amount in the
        /// currency named by <see cref="Currency"/>, and a count as a plain number.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal Value { get; set; }

        public StatutoryValueKind Kind { get; set; } = StatutoryValueKind.Percentage;

        /// <summary>Set for monetary parameters. Null for rates and counts.</summary>
        [StringLength(10)]
        public string? Currency { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Effective from")]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;

        /// <summary>Null while this is the current value. Set when a later value supersedes it.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Effective to")]
        public DateTime? EffectiveTo { get; set; }

        /// <summary>
        /// Where the figure comes from — the Act, Statutory Instrument or gazette notice. Without
        /// this nobody can check the number a year later, and payroll figures must be checkable.
        /// </summary>
        [StringLength(300)]
        public string? Authority { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public int? UpdatedById { get; set; }
        [ValidateNever] public User? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        public bool IsCurrent => EffectiveFrom <= DateTime.Today && (EffectiveTo == null || EffectiveTo >= DateTime.Today);
    }

    public enum StatutoryValueKind
    {
        /// <summary>A rate expressed as a percentage — 4.5 means 4.5%.</summary>
        Percentage,
        /// <summary>A money amount in <see cref="StatutoryParameter.Currency"/>.</summary>
        Amount,
        /// <summary>A plain count, e.g. a number of days.</summary>
        Days,
        /// <summary>A multiplier, e.g. 1.5 for overtime.</summary>
        Multiplier
    }

    /// <summary>
    /// The keys the payroll and leave engines look up. Kept as constants so a typo is a compile
    /// error rather than a silently missing deduction.
    /// </summary>
    public static class StatutoryKeys
    {
        // ── NSSA (National Social Security Authority Act [Chapter 17:04]) ─────────
        /// <summary>Pension and Other Benefits Scheme — employee share of insurable earnings.</summary>
        public const string NssaPobsEmployeeRate = "NSSA.POBS.EmployeeRate";
        /// <summary>Pension and Other Benefits Scheme — employer share.</summary>
        public const string NssaPobsEmployerRate = "NSSA.POBS.EmployerRate";
        /// <summary>Ceiling on monthly insurable earnings for POBS. Re-gazetted as the currency moves.</summary>
        public const string NssaInsurableEarningsCeiling = "NSSA.POBS.InsurableEarningsCeiling";
        /// <summary>Accident Prevention and Workers Compensation Scheme — employer only, industry-rated.</summary>
        public const string NssaApwcsEmployerRate = "NSSA.APWCS.EmployerRate";

        // ── Tax (Income Tax Act [Chapter 23:06] and the annual Finance Act) ───────
        /// <summary>AIDS levy, charged on the PAYE payable rather than on gross pay.</summary>
        public const string AidsLevyRate = "Tax.AidsLevyRate";

        /// <summary>
        /// The proportion of a medical aid contribution allowed as a credit against tax payable.
        /// A credit against the tax, not a deduction from income — the distinction changes the
        /// answer, so it is held as its own parameter rather than folded into a rate elsewhere.
        /// </summary>
        public const string MedicalAidCreditRate = "Tax.MedicalAidCreditRate";

        // ── Employer levies ──────────────────────────────────────────────────────
        /// <summary>Manpower development levy on the gross wage bill (Manpower Planning and Development Act).</summary>
        public const string ZimdefRate = "Levy.ZimdefRate";
        /// <summary>Standards development levy on gross wages.</summary>
        public const string StandardsDevelopmentLevyRate = "Levy.StandardsDevelopmentRate";

        // ── Leave (Labour Act [Chapter 28:01]) ───────────────────────────────────
        /// <summary>Vacation leave accrued per month of service — s.14A sets a minimum of 1/12 of 30 days.</summary>
        public const string VacationLeaveAccrualPerMonth = "Leave.VacationAccrualPerMonth";
        /// <summary>Sick leave on full pay in a twelve-month period — s.14.</summary>
        public const string SickLeaveFullPayDays = "Leave.SickFullPayDays";
        /// <summary>Further sick leave on half pay once full-pay entitlement is exhausted — s.14.</summary>
        public const string SickLeaveHalfPayDays = "Leave.SickHalfPayDays";
        /// <summary>Fully paid maternity leave — s.18.</summary>
        public const string MaternityLeaveDays = "Leave.MaternityDays";
        /// <summary>Minimum months of service before maternity leave may be taken — s.18.</summary>
        public const string MaternityQualifyingMonths = "Leave.MaternityQualifyingMonths";
        /// <summary>Paternity leave, introduced by the Labour Amendment Act, 2023.</summary>
        public const string PaternityLeaveDays = "Leave.PaternityDays";
        /// <summary>Special leave on full pay per year — s.14B (bereavement, detention, examinations).</summary>
        public const string SpecialLeaveDays = "Leave.SpecialDays";
        /// <summary>Maximum vacation days that may be carried into the next leave cycle.</summary>
        public const string MaxLeaveCarryOverDays = "Leave.MaxCarryOverDays";

        // ── Working time and overtime (largely set by NEC agreements, not the Act) ─
        public const string StandardHoursPerWeek = "Time.StandardHoursPerWeek";
        public const string StandardHoursPerDay = "Time.StandardHoursPerDay";
        public const string OvertimeMultiplier = "Time.OvertimeMultiplier";
        public const string RestDayMultiplier = "Time.RestDayMultiplier";
        public const string PublicHolidayMultiplier = "Time.PublicHolidayMultiplier";

        // ── Termination (Labour Act [Chapter 28:01] s.12 and s.12C) ──────────────
        /// <summary>Months of salary payable per two years of service on retrenchment — s.12C.</summary>
        public const string RetrenchmentMonthsPerTwoYears = "Termination.RetrenchmentMonthsPerTwoYears";
    }

    /// <summary>
    /// A PAYE band. Zimbabwe operates parallel currencies, so bands are held per currency and per
    /// pay period, and every set carries the Finance Act that introduced it.
    /// </summary>
    public class PayeTaxBand
    {
        public int Id { get; set; }

        [Required, StringLength(10)]
        public string Currency { get; set; } = "USD";

        /// <summary>The period the thresholds are expressed in. Monthly is the usual published form.</summary>
        public PayPeriod Period { get; set; } = PayPeriod.Monthly;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "From")]
        public decimal FromAmount { get; set; }

        /// <summary>Null for the top band, which is open-ended.</summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "To")]
        public decimal? ToAmount { get; set; }

        /// <summary>Marginal rate as a percentage — 20 means 20%.</summary>
        [Column(TypeName = "decimal(9,4)")]
        public decimal Rate { get; set; }

        /// <summary>
        /// Amount deducted after applying the marginal rate, which is how ZIMRA publishes the
        /// tables. Lets the whole calculation be (income × rate) − deduction for the matched band.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Deduction { get; set; }

        [DataType(DataType.Date)]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime? EffectiveTo { get; set; }

        [StringLength(300)]
        public string? Authority { get; set; }
    }

    public enum PayPeriod { Monthly, Weekly, Fortnightly, Annual, Daily }

    /// <summary>
    /// A Zimbabwean public holiday. Held as data because the fixed dates shift when they fall on a
    /// Sunday (the following Monday becomes the holiday), Easter moves every year, and the
    /// President may declare a one-off holiday.
    /// </summary>
    public class PublicHoliday
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        /// <summary>
        /// True when this is the observed day rather than the actual date — the Public Holidays and
        /// Prohibition of Business Act moves a holiday falling on a Sunday to the Monday.
        /// </summary>
        public bool IsObservedShift { get; set; }

        /// <summary>Declared for a specific occasion rather than recurring annually.</summary>
        public bool IsOneOff { get; set; }

        [StringLength(300)]
        public string? Notes { get; set; }
    }
}
