using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Risk & Opportunity enumerations (ISO 9001 cl. 6.1 / ISO 27001 cl. 6.1 & 8.2) ──
    public enum RiskCategory
    {
        Strategic, Operational, InformationSecurity, Quality, Compliance,
        Financial, HealthAndSafety, Environmental, Supplier, Project, Reputational, Other
    }

    public enum RiskTreatment { Mitigate, Accept, Transfer, Avoid }
    public enum RiskStatus { Identified, Assessed, TreatmentInProgress, Monitoring, Closed }
    public enum OpportunityStatus { Identified, Evaluating, Pursuing, Realised, Declined, Closed }

    /// <summary>
    /// A risk in the register. Likelihood and Impact are 1–5 scales; Score = L × I (1–25) and the
    /// band (Low/Medium/High/Critical) drives the heat map. Optionally linked to an existing Asset.
    /// </summary>
    public class Risk
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"RSK-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public RiskCategory Category { get; set; } = RiskCategory.Operational;
        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [StringLength(2500)]
        public string? Description { get; set; }

        [Display(Name = "Related Asset")]
        public int? AssetId { get; set; }
        [ValidateNever] public Asset? Asset { get; set; }

        [StringLength(1000)]
        public string? Threat { get; set; }
        [StringLength(1000)]
        public string? Vulnerability { get; set; }

        [Range(1, 5)]
        public int Likelihood { get; set; } = 1;
        [Range(1, 5)]
        public int Impact { get; set; } = 1;

        public RiskTreatment Treatment { get; set; } = RiskTreatment.Mitigate;

        [StringLength(3000), Display(Name = "Treatment Plan")]
        public string? TreatmentPlan { get; set; }

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        [Range(1, 5), Display(Name = "Residual Likelihood")]
        public int? ResidualLikelihood { get; set; }
        [Range(1, 5), Display(Name = "Residual Impact")]
        public int? ResidualImpact { get; set; }

        public RiskStatus Status { get; set; } = RiskStatus.Identified;

        [Display(Name = "Review Date"), DataType(DataType.Date)]
        public DateTime? ReviewDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }
        public DateTime? ClosedAt { get; set; }

        [NotMapped] public int Score => Likelihood * Impact;
        [NotMapped] public int? ResidualScore =>
            ResidualLikelihood.HasValue && ResidualImpact.HasValue ? ResidualLikelihood * ResidualImpact : null;
        [NotMapped] public RiskBand Band => RiskScoring.BandFor(Score);
        [NotMapped] public RiskBand? ResidualBand => ResidualScore.HasValue ? RiskScoring.BandFor(ResidualScore.Value) : null;
        [NotMapped] public bool IsReviewDue => Status != RiskStatus.Closed && ReviewDate.HasValue && ReviewDate.Value.Date <= DateTime.Now.Date;
    }

    /// <summary>An opportunity in the register (ISO 9001/27001 cl. 6.1 — risks &amp; opportunities).</summary>
    public class Opportunity
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"OPP-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2500)]
        public string? Description { get; set; }

        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [StringLength(2000), Display(Name = "Expected Benefit")]
        public string? Benefit { get; set; }

        [Range(1, 5)]
        public int Likelihood { get; set; } = 3;
        [Range(1, 5), Display(Name = "Benefit / Impact")]
        public int BenefitScore { get; set; } = 3;

        [StringLength(3000), Display(Name = "Action Plan")]
        public string? ActionPlan { get; set; }

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }
        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        public OpportunityStatus Status { get; set; } = OpportunityStatus.Identified;

        [Display(Name = "Target Date"), DataType(DataType.Date)]
        public DateTime? TargetDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        [NotMapped] public int Score => Likelihood * BenefitScore;
    }

    public enum RiskBand { Low, Medium, High, Critical }

    /// <summary>Central 5×5 risk banding so the register and heat map agree.</summary>
    public static class RiskScoring
    {
        public static RiskBand BandFor(int score) => score switch
        {
            <= 4 => RiskBand.Low,
            <= 9 => RiskBand.Medium,
            <= 15 => RiskBand.High,
            _ => RiskBand.Critical
        };

        public static string CssClass(RiskBand band) => band switch
        {
            RiskBand.Low => "b-low",
            RiskBand.Medium => "b-medium",
            RiskBand.High => "b-high",
            _ => "b-critical"
        };
    }
}
