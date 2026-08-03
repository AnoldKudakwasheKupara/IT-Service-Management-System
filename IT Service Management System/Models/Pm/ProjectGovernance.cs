using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Pm
{
    /// <summary>
    /// An entry in the project risk register. Probability × impact (1–5 each) gives a 1–25 score
    /// that positions the risk on the heat map.
    /// </summary>
    public class ProjectRisk
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        [Required, StringLength(250)]
        [Display(Name = "Risk")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [StringLength(120)]
        public string? Category { get; set; }

        [Range(1, 5)]
        public int Probability { get; set; } = 3;

        [Range(1, 5)]
        public int Impact { get; set; } = 3;

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        [StringLength(2000)]
        public string? Mitigation { get; set; }

        public RiskResponse Response { get; set; } = RiskResponse.Mitigate;

        [StringLength(2000)]
        [Display(Name = "Response plan")]
        public string? ResponsePlan { get; set; }

        /// <summary>What we do if the risk materialises anyway.</summary>
        [StringLength(2000)]
        public string? ContingencyPlan { get; set; }

        public PmRiskStatus Status { get; set; } = PmRiskStatus.Identified;

        /// <summary>Residual score expected once the mitigation is in place (1–25).</summary>
        [Range(1, 25)]
        public int? TargetScore { get; set; }

        [DataType(DataType.Date)] public DateTime? IdentifiedDate { get; set; } = DateTime.Today;
        [DataType(DataType.Date)] public DateTime? ReviewDate { get; set; }
        [DataType(DataType.Date)] public DateTime? ClosedDate { get; set; }

        /// <summary>Money set aside against this risk.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ContingencyAmount { get; set; }

        public int CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string Reference => $"RSK-{Id:D5}";

        [NotMapped]
        public int Score => Math.Clamp(Probability, 1, 5) * Math.Clamp(Impact, 1, 5);

        /// <summary>Heat-map band derived from the score.</summary>
        [NotMapped]
        public string Band => Score switch
        {
            >= 15 => "Critical",
            >= 10 => "High",
            >= 5 => "Medium",
            _ => "Low"
        };

        /// <summary>High-scoring and still open — surfaces on the executive dashboard.</summary>
        [NotMapped]
        public bool NeedsAttention =>
            Status is not (PmRiskStatus.Closed) && Score >= 10;
    }

    /// <summary>A problem that has already materialised on the project and needs resolving.</summary>
    public class ProjectIssue
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        /// <summary>Set when the issue is a risk that came true.</summary>
        public int? RaisedFromRiskId { get; set; }
        [ValidateNever] public ProjectRisk? RaisedFromRisk { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public IssueStatus Status { get; set; } = IssueStatus.Open;

        public int? AssignedToId { get; set; }
        [ValidateNever] public User? AssignedTo { get; set; }

        [StringLength(2000)]
        public string? RootCause { get; set; }

        [StringLength(2000)]
        public string? Resolution { get; set; }

        /// <summary>Set when the issue was raised by the client through the portal.</summary>
        public bool RaisedByClient { get; set; }

        [DataType(DataType.Date)] public DateTime? DueDate { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        public int RaisedById { get; set; }
        [ValidateNever] public User? RaisedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string Reference => $"ISS-{Id:D5}";

        [NotMapped]
        public bool IsOpen => Status is not (IssueStatus.Resolved or IssueStatus.Closed);

        [NotMapped]
        public bool IsOverdue => IsOpen && DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
    }

    /// <summary>
    /// A formal change to project scope, schedule or cost. Captures the impact assessment and the
    /// approval decision; approved changes feed the project's baseline and budget.
    /// </summary>
    public class ProjectChangeRequest
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(4000)]
        [Display(Name = "Reason for change")]
        public string Reason { get; set; } = string.Empty;

        [StringLength(4000)]
        [Display(Name = "Impact assessment")]
        public string? ImpactAssessment { get; set; }

        public ChangeImpactLevel ImpactLevel { get; set; } = ChangeImpactLevel.Moderate;

        /// <summary>Cost delta — positive adds to the budget, negative releases funds.</summary>
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Cost impact")]
        public decimal CostImpact { get; set; }

        /// <summary>Schedule delta in days — positive pushes the end date out.</summary>
        [Display(Name = "Timeline effect (days)")]
        public int ScheduleImpactDays { get; set; }

        public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Draft;

        public int RequestedById { get; set; }
        [ValidateNever] public User? RequestedBy { get; set; }

        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }
        public DateTime? DecidedAt { get; set; }

        [StringLength(1000)]
        public string? DecisionNote { get; set; }

        [StringLength(4000)]
        [Display(Name = "Implementation plan")]
        public string? ImplementationPlan { get; set; }

        public DateTime? ImplementedAt { get; set; }

        /// <summary>Set once the approved cost/schedule impact has been applied to the project.</summary>
        public bool AppliedToBaseline { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string Reference => $"PCR-{Id:D5}";
    }

    /// <summary>A quality control activity — an inspection, test, review or acceptance check.</summary>
    public class QualityCheck
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? DeliverableId { get; set; }
        [ValidateNever] public Deliverable? Deliverable { get; set; }

        public int? TaskId { get; set; }
        [ValidateNever] public ProjectTask? Task { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        public QualityCheckType Type { get; set; } = QualityCheckType.Inspection;

        [StringLength(2000)]
        [Display(Name = "Acceptance criteria")]
        public string? AcceptanceCriteria { get; set; }

        public QualityResult Result { get; set; } = QualityResult.Pending;

        [StringLength(2000)]
        public string? Findings { get; set; }

        /// <summary>Action required when the check fails.</summary>
        [StringLength(2000)]
        public string? CorrectiveAction { get; set; }

        public int? InspectorId { get; set; }
        [ValidateNever] public User? Inspector { get; set; }

        [DataType(DataType.Date)] public DateTime? ScheduledDate { get; set; }
        [DataType(DataType.Date)] public DateTime? PerformedDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// One step of a multi-level approval chain. Steps for the same subject share a
    /// <see cref="SubjectId"/> and are worked through in <see cref="Level"/> order.
    /// </summary>
    public class ProjectApproval
    {
        public int Id { get; set; }

        public int? ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public ApprovalSubject Subject { get; set; }

        /// <summary>Primary key of the record under approval (an expense id, a change-request id…).</summary>
        public int SubjectId { get; set; }

        [StringLength(250)]
        public string? SubjectTitle { get; set; }

        /// <summary>1-based step number. Level 2 only opens once level 1 is approved.</summary>
        public int Level { get; set; } = 1;

        public int ApproverId { get; set; }
        [ValidateNever] public User? Approver { get; set; }

        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

        [StringLength(1000)]
        public string? Comment { get; set; }

        /// <summary>Amount at stake, shown to the approver and used for threshold routing.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public DateTime? DecidedAt { get; set; }

        public int RequestedById { get; set; }
        [ValidateNever] public User? RequestedBy { get; set; }

        /// <summary>Set when the approver hands the step to someone else.</summary>
        public int? DelegatedToId { get; set; }
        [ValidateNever] public User? DelegatedTo { get; set; }

        [NotMapped]
        public bool IsPending => Status == ApprovalStatus.Pending;
    }

    /// <summary>A key performance indicator tracked at project or portfolio level.</summary>
    public class ProjectKpi
    {
        public int Id { get; set; }

        /// <summary>Null for portfolio-wide KPIs.</summary>
        public int? ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(40)]
        public string? Unit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TargetValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualValue { get; set; }

        /// <summary>True when a higher number is better (e.g. satisfaction); false for variance-style KPIs.</summary>
        public bool HigherIsBetter { get; set; } = true;

        [DataType(DataType.Date)] public DateTime PeriodStart { get; set; } = DateTime.Today;
        [DataType(DataType.Date)] public DateTime PeriodEnd { get; set; } = DateTime.Today;

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public int AchievementPercent =>
            TargetValue == 0 ? 0
            : HigherIsBetter
                ? (int)Math.Clamp(Math.Round(ActualValue / TargetValue * 100), 0, 999)
                : (int)Math.Clamp(Math.Round(TargetValue / (ActualValue == 0 ? TargetValue : ActualValue) * 100), 0, 999);

        [NotMapped]
        public bool OnTarget => HigherIsBetter ? ActualValue >= TargetValue : ActualValue <= TargetValue;
    }
}
