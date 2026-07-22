using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Compliance Register enumerations (ISO 9001 cl. 9.1.2 / ISO 27001 cl. 9.1 & A.18) ──
    public enum ComplianceType { Legal, Regulatory, Contractual, Standard, Internal }
    public enum ComplianceStatus { Compliant, PartiallyCompliant, NonCompliant, NotAssessed, UnderReview }

    /// <summary>
    /// A compliance obligation the organisation must meet (legal, regulatory, contractual, standard).
    /// Named to avoid clashing with the existing security "Compliance" module.
    /// </summary>
    public class ComplianceObligation
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"OBL-{Id:D5}";

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        public ComplianceType Type { get; set; } = ComplianceType.Legal;
        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [StringLength(3000)]
        public string? Description { get; set; }

        [StringLength(3000), Display(Name = "Requirement")]
        public string? Requirement { get; set; }

        [StringLength(200), Display(Name = "Issuing Authority")]
        public string? Authority { get; set; }

        [StringLength(150), Display(Name = "Legal / Reg. Reference")]
        public string? LegalReference { get; set; }

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        public ComplianceStatus Status { get; set; } = ComplianceStatus.NotAssessed;

        [Display(Name = "Last Assessed"), DataType(DataType.Date)]
        public DateTime? LastAssessedDate { get; set; }
        [Display(Name = "Next Review"), DataType(DataType.Date)]
        public DateTime? NextReviewDate { get; set; }

        [StringLength(3000), Display(Name = "Evidence / Notes")]
        public string? EvidenceNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        [NotMapped] public bool IsReviewDue =>
            NextReviewDate.HasValue && NextReviewDate.Value.Date <= DateTime.Now.Date;
    }
}
