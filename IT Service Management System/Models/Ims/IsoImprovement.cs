using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Continuous Improvement enumerations (ISO 9001 cl. 10.3 / ISO 27001 cl. 10.2) ──
    public enum ImprovementType { Kaizen, Suggestion, ProcessImprovement, CostSaving, Innovation, LessonLearned }
    public enum ImprovementStatus { Proposed, UnderReview, Approved, InProgress, Implemented, Rejected, Closed }

    /// <summary>An entry in the continuous-improvement register.</summary>
    public class Improvement
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"IMP-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public ImprovementType Type { get; set; } = ImprovementType.Suggestion;
        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [Required, StringLength(3000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(2000), Display(Name = "Expected Benefit")]
        public string? ExpectedBenefit { get; set; }

        [Display(Name = "Proposed By")]
        public int? ProposedById { get; set; }
        [ValidateNever] public User? ProposedBy { get; set; }

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        public ImprovementStatus Status { get; set; } = ImprovementStatus.Proposed;

        [Display(Name = "Target Date"), DataType(DataType.Date)]
        public DateTime? TargetDate { get; set; }
        [Display(Name = "Completed Date"), DataType(DataType.Date)]
        public DateTime? CompletedDate { get; set; }

        [StringLength(2000), Display(Name = "Actual Benefit")]
        public string? ActualBenefit { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        [NotMapped] public bool IsClosed => Status is ImprovementStatus.Implemented or ImprovementStatus.Rejected or ImprovementStatus.Closed;
    }
}
