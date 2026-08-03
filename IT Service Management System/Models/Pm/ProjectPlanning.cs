using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Pm
{
    /// <summary>A stage of the project lifecycle (Initiation, Design, Build, Handover…).</summary>
    public class ProjectPhase
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        /// <summary>Display order along the timeline.</summary>
        public int Sequence { get; set; }

        [DataType(DataType.Date)] public DateTime? StartDate { get; set; }
        [DataType(DataType.Date)] public DateTime? EndDate { get; set; }

        public PhaseStatus Status { get; set; } = PhaseStatus.NotStarted;

        [Range(0, 100)]
        public int ProgressPercent { get; set; }

        [ValidateNever] public ICollection<WbsItem> WbsItems { get; set; } = new List<WbsItem>();
        [ValidateNever] public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    }

    /// <summary>
    /// A node in the Work Breakdown Structure — a hierarchical decomposition of the project scope.
    /// Leaf nodes are where tasks and effort estimates attach.
    /// </summary>
    public class WbsItem
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? ParentId { get; set; }
        [ValidateNever] public WbsItem? Parent { get; set; }
        [ValidateNever] public ICollection<WbsItem> Children { get; set; } = new List<WbsItem>();

        public int? PhaseId { get; set; }
        [ValidateNever] public ProjectPhase? Phase { get; set; }

        /// <summary>Outline number, e.g. "1.2.3". Recomputed when the tree is reordered.</summary>
        [StringLength(40)]
        public string WbsCode { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public int Sequence { get; set; }

        [DataType(DataType.Date)] public DateTime? StartDate { get; set; }
        [DataType(DataType.Date)] public DateTime? EndDate { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal EstimatedHours { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedCost { get; set; }

        [Range(0, 100)]
        public int ProgressPercent { get; set; }

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        [ValidateNever] public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    }

    /// <summary>A dated checkpoint marking a significant achievement (go-live, sign-off…).</summary>
    public class Milestone
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? PhaseId { get; set; }
        [ValidateNever] public ProjectPhase? Phase { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; } = DateTime.Today;

        /// <summary>Original committed date, kept for slippage reporting.</summary>
        [DataType(DataType.Date)] public DateTime? BaselineDate { get; set; }

        [DataType(DataType.Date)] public DateTime? AchievedDate { get; set; }

        public MilestoneStatus Status { get; set; } = MilestoneStatus.Planned;

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        /// <summary>Whether the client must formally sign this milestone off.</summary>
        public bool RequiresClientApproval { get; set; }

        public bool ClientApproved { get; set; }
        public DateTime? ClientApprovedAt { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Days late, once achieved (or days late so far, if still open and overdue).</summary>
        [NotMapped]
        public int SlippageDays
        {
            get
            {
                var reference = AchievedDate ?? DateTime.Today;
                var baseline = BaselineDate ?? DueDate;
                var slip = (int)(reference.Date - baseline.Date).TotalDays;
                return slip > 0 ? slip : 0;
            }
        }

        [NotMapped]
        public bool IsOverdue => Status is MilestoneStatus.Planned or MilestoneStatus.AtRisk && DueDate.Date < DateTime.Today;
    }

    /// <summary>A tangible output the project must produce and hand over.</summary>
    public class Deliverable
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? MilestoneId { get; set; }
        [ValidateNever] public Milestone? Milestone { get; set; }

        public int? PhaseId { get; set; }
        [ValidateNever] public ProjectPhase? Phase { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        /// <summary>The conditions the deliverable must meet to be accepted.</summary>
        [StringLength(2000)]
        public string? AcceptanceCriteria { get; set; }

        public DeliverableStatus Status { get; set; } = DeliverableStatus.NotStarted;

        [DataType(DataType.Date)] public DateTime? DueDate { get; set; }
        [DataType(DataType.Date)] public DateTime? SubmittedDate { get; set; }
        [DataType(DataType.Date)] public DateTime? AcceptedDate { get; set; }

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        public int? AcceptedById { get; set; }
        [ValidateNever] public User? AcceptedBy { get; set; }

        [StringLength(1000)]
        public string? AcceptanceNotes { get; set; }

        /// <summary>Included in the closure checklist — a deliverable that must be signed off to close.</summary>
        public bool IsClosureItem { get; set; } = true;
    }
}
