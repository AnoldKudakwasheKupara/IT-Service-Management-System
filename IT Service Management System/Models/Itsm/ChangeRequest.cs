using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Itsm
{
    /// <summary>
    /// ITIL Change Request — a controlled change to a CI/service, with risk/impact, an approval
    /// gate, a scheduled window, and implementation + backout plans. Change success is recorded
    /// for the change success-rate KPI.
    /// </summary>
    public class ChangeRequest
    {
        public int Id { get; set; }

        [NotMapped]
        public string ChangeRef => $"CHG-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public ChangeType Type { get; set; } = ChangeType.Normal;
        public ChangeStatus Status { get; set; } = ChangeStatus.Draft;
        public ChangeRisk Risk { get; set; } = ChangeRisk.Medium;
        public ChangeImpact Impact { get; set; } = ChangeImpact.Medium;

        [StringLength(4000)]
        public string? ImplementationPlan { get; set; }
        [StringLength(4000)]
        public string? BackoutPlan { get; set; }
        [StringLength(4000)]
        public string? TestPlan { get; set; }

        public DateTime? ScheduledStart { get; set; }
        public DateTime? ScheduledEnd { get; set; }

        public int? ConfigurationItemId { get; set; }
        [ValidateNever]
        public ConfigurationItem? ConfigurationItem { get; set; }

        public int? ProblemId { get; set; }
        [ValidateNever]
        public Problem? Problem { get; set; }

        public int? AssignedToId { get; set; }
        [ValidateNever]
        public User? AssignedTo { get; set; }

        public int? ApprovedById { get; set; }
        [ValidateNever]
        public User? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        [StringLength(1000)]
        public string? ApprovalNotes { get; set; }

        public int CreatedById { get; set; }
        [ValidateNever]
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        /// <summary>Set when the change is closed — did it succeed? Drives the change success rate.</summary>
        public bool? ImplementedSuccessfully { get; set; }

        [NotMapped]
        public bool IsClosed => Status is ChangeStatus.Closed or ChangeStatus.Cancelled or ChangeStatus.Rejected;
    }
}
