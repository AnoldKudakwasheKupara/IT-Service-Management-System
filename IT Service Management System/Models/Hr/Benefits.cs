using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// A benefit the employer offers — medical aid, an occupational pension, funeral cover, group
    /// life, a housing or transport allowance, a company vehicle.
    /// <para>
    /// The tax treatment is part of the plan rather than an afterthought, because in Zimbabwe it
    /// decides what the employee actually receives. A benefit in kind is taxable in the employee's
    /// hands under the Income Tax Act [Chapter 23:06]; contributions to an approved pension fund are
    /// deductible within a limit; medical aid contributions attract a tax credit rather than a
    /// deduction. Recording a benefit without recording which of those applies produces a payslip
    /// that is wrong in a way nobody notices until ZIMRA does.
    /// </para>
    /// </summary>
    public class BenefitPlan
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [StringLength(150)]
        [Display(Name = "Provider")]
        public string? Provider { get; set; }

        public BenefitCategory Category { get; set; } = BenefitCategory.Other;

        /// <summary>How the benefit is treated for PAYE. See <see cref="BenefitTaxTreatment"/>.</summary>
        [Display(Name = "Tax treatment")]
        public BenefitTaxTreatment TaxTreatment { get; set; } = BenefitTaxTreatment.TaxableBenefit;

        /// <summary>The provision the treatment comes from, so it can be checked rather than assumed.</summary>
        [StringLength(250)]
        public string? TaxAuthority { get; set; }

        // ── Cost ─────────────────────────────────────────────────────────────────
        [Display(Name = "How the contribution is set")]
        public ContributionBasis Basis { get; set; } = ContributionBasis.FixedAmount;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Employer contribution")]
        public decimal EmployerAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Employee contribution")]
        public decimal EmployeeAmount { get; set; }

        /// <summary>
        /// Where the contribution is a percentage of basic salary, these carry the rate instead of
        /// the amount. Kept separate so a plan cannot be half one and half the other by accident.
        /// </summary>
        [Column(TypeName = "decimal(9,4)")]
        [Display(Name = "Employer rate (% of basic)")]
        public decimal EmployerRate { get; set; }

        [Column(TypeName = "decimal(9,4)")]
        [Display(Name = "Employee rate (% of basic)")]
        public decimal EmployeeRate { get; set; }

        [StringLength(3)] public string Currency { get; set; } = "USD";

        // ── Eligibility ──────────────────────────────────────────────────────────
        [Display(Name = "Minimum months of service")]
        [Range(0, 120)]
        public int QualifyingMonths { get; set; }

        [Display(Name = "Available to")]
        public EmploymentType? AvailableTo { get; set; }

        [Display(Name = "Dependants may be covered")]
        public bool AllowsDependants { get; set; }

        [Display(Name = "Maximum dependants")]
        [Range(0, 20)]
        public int MaxDependants { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Additional cost per dependant")]
        public decimal CostPerDependant { get; set; }

        /// <summary>
        /// Enrolment is automatic where the benefit is a condition of employment rather than a
        /// choice — an occupational pension usually is.
        /// </summary>
        [Display(Name = "Enrolment is automatic")]
        public bool IsAutomatic { get; set; }

        public bool IsActive { get; set; } = true;

        [DataType(DataType.Date)]
        [Display(Name = "Effective from")]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Effective to")]
        public DateTime? EffectiveTo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<BenefitEnrolment> Enrolments { get; set; } = new List<BenefitEnrolment>();

        // ── Derived ──────────────────────────────────────────────────────────────
        [NotMapped]
        public bool IsCurrent => EffectiveFrom <= DateTime.Today
            && (EffectiveTo == null || EffectiveTo >= DateTime.Today) && IsActive;

        [NotMapped]
        public string CostSummary => Basis == ContributionBasis.PercentOfBasic
            ? $"Employer {EmployerRate:0.##}%, employee {EmployeeRate:0.##}% of basic"
            : $"{Currency} {EmployerAmount:N2} employer, {Currency} {EmployeeAmount:N2} employee";
    }

    public enum BenefitCategory
    {
        MedicalAid,
        Pension,
        FuneralCover,
        GroupLifeAssurance,
        HousingAllowance,
        TransportAllowance,
        Vehicle,
        SchoolFees,
        Loan,
        Other
    }

    public enum ContributionBasis { FixedAmount, PercentOfBasic }

    /// <summary>
    /// How a benefit is treated for PAYE, which decides what actually lands in the employee's hands.
    /// </summary>
    public enum BenefitTaxTreatment
    {
        /// <summary>
        /// A benefit in kind, taxable in the employee's hands. Its value is added to taxable income
        /// before PAYE is worked out.
        /// </summary>
        [Display(Name = "Taxable benefit in kind")]
        TaxableBenefit,

        /// <summary>
        /// An employee contribution deductible before PAYE, within the statutory limit for an
        /// approved fund. NSSA and an approved occupational pension sit here.
        /// </summary>
        [Display(Name = "Deductible before PAYE (approved fund)")]
        DeductibleContribution,

        /// <summary>
        /// Attracts a tax credit against PAYE rather than a deduction from income — the treatment
        /// medical aid contributions receive. The credit is a proportion of the contribution.
        /// </summary>
        [Display(Name = "Attracts a medical aid tax credit")]
        MedicalAidCredit,

        /// <summary>Not taxable and not deductible.</summary>
        [Display(Name = "Not taxable")]
        Exempt
    }

    /// <summary>
    /// One employee's membership of one plan.
    /// <para>
    /// Enrolments are effective-dated and ended rather than deleted. Somebody's cover in March is a
    /// fact about March, and a claim made then is judged against what they had then.
    /// </para>
    /// </summary>
    public class BenefitEnrolment
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"BEN-{Id:D5}";

        public int PlanId { get; set; }
        [ValidateNever] public BenefitPlan? Plan { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Cover from")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Cover to")]
        public DateTime? EndDate { get; set; }

        [StringLength(80)]
        [Display(Name = "Membership number")]
        public string? MembershipNumber { get; set; }

        /// <summary>
        /// Overrides the plan's contribution for this member, where their terms differ. Null means
        /// the plan's own figure applies, so a change to the plan reaches them.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Employee contribution override")]
        public decimal? EmployeeAmountOverride { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Employer contribution override")]
        public decimal? EmployerAmountOverride { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [Display(Name = "Ended because")]
        [StringLength(500)]
        public string? EndReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<BenefitDependant> Dependants { get; set; } = new List<BenefitDependant>();

        [NotMapped]
        public bool IsActive => StartDate <= DateTime.Today && (EndDate == null || EndDate >= DateTime.Today);
    }

    /// <summary>
    /// Someone covered under an employee's benefit.
    /// <para>
    /// Only what the cover actually needs is held: a name, the relationship, and a date of birth,
    /// because a child's cover usually ends at an age. Nothing else about a member's family belongs
    /// on an employer's system.
    /// </para>
    /// </summary>
    public class BenefitDependant
    {
        public int Id { get; set; }

        public int EnrolmentId { get; set; }
        [ValidateNever] public BenefitEnrolment? Enrolment { get; set; }

        [Required, StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required, StringLength(40)]
        public string Relationship { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Cover from")]
        public DateTime AddedOn { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Cover to")]
        public DateTime? RemovedOn { get; set; }

        [NotMapped]
        public bool IsActive => RemovedOn == null || RemovedOn >= DateTime.Today;

        [NotMapped]
        public int? Age => DateOfBirth.HasValue
            ? (int)((DateTime.Today - DateOfBirth.Value).TotalDays / 365.2425)
            : null;
    }
}
