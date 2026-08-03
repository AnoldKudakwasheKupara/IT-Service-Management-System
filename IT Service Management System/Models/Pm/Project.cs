using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Pm
{
    /// <summary>
    /// A project in the organisation's portfolio — the root record every other project-management
    /// entity (tasks, milestones, budget, risks, documents…) hangs off.
    /// </summary>
    public class Project
    {
        public int Id { get; set; }

        /// <summary>Human-readable code, e.g. PRJ-2026-014. Auto-generated when left blank.</summary>
        [StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        [Display(Name = "Project name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? Client { get; set; }

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        /// <summary>Executive accountable for the business case.</summary>
        public int? SponsorId { get; set; }
        [ValidateNever] public User? Sponsor { get; set; }

        /// <summary>Day-to-day owner. Gets edit rights on the project regardless of role.</summary>
        public int? ProjectManagerId { get; set; }
        [ValidateNever] public User? ProjectManager { get; set; }

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
        public ProjectCategory Category { get; set; } = ProjectCategory.Other;
        public ProjectType Type { get; set; } = ProjectType.Internal;
        public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

        [DataType(DataType.Date)] public DateTime? StartDate { get; set; }
        [DataType(DataType.Date)] public DateTime? EndDate { get; set; }

        /// <summary>Baseline finish captured at approval — the yardstick for schedule variance.</summary>
        [DataType(DataType.Date)] public DateTime? BaselineEndDate { get; set; }

        [DataType(DataType.Date)] public DateTime? ActualStartDate { get; set; }
        [DataType(DataType.Date)] public DateTime? ActualEndDate { get; set; }

        /// <summary>Planned duration in working days. Derived from the dates when not set explicitly.</summary>
        public int? EstimatedDurationDays { get; set; }

        [Range(0, 100)]
        [Display(Name = "Overall progress %")]
        public int ProgressPercent { get; set; }

        /// <summary>When true, progress is rolled up from tasks instead of being entered by hand.</summary>
        public bool AutoCalculateProgress { get; set; } = true;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Budget { get; set; }

        /// <summary>Approved change-order value added to the original budget.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ApprovedChangeValue { get; set; }

        [StringLength(10)]
        public string Currency { get; set; } = "USD";

        [StringLength(200)]
        public string? Location { get; set; }

        /// <summary>Free-text, comma-separated tags for portfolio filtering.</summary>
        [StringLength(500)]
        public string? Tags { get; set; }

        public ProjectHealth Health { get; set; } = ProjectHealth.Green;

        /// <summary>Short narrative shown on the executive dashboard.</summary>
        [StringLength(1000)]
        public string? HealthNote { get; set; }

        /// <summary>Template this project was created from, when applicable.</summary>
        public int? CreatedFromTemplateId { get; set; }

        public int CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }

        public bool IsDeleted { get; set; }

        // ── Derived ──────────────────────────────────────────────────────────────
        [NotMapped]
        public string Reference => string.IsNullOrWhiteSpace(Code) ? $"PRJ-{Id:D5}" : Code;

        /// <summary>Original budget plus any approved change orders.</summary>
        [NotMapped]
        public decimal TotalBudget => Budget + ApprovedChangeValue;

        [NotMapped]
        public bool IsOpen => Status is not (ProjectStatus.Completed or ProjectStatus.Cancelled or ProjectStatus.Archived);

        /// <summary>Past its end date while still open.</summary>
        [NotMapped]
        public bool IsOverdue => IsOpen && EndDate.HasValue && EndDate.Value.Date < DateTime.Today;

        [NotMapped]
        public int? DaysRemaining => EndDate.HasValue ? (int)(EndDate.Value.Date - DateTime.Today).TotalDays : null;

        /// <summary>Percentage of the planned schedule that has elapsed — compare against ProgressPercent.</summary>
        [NotMapped]
        public int SchedulePercentElapsed
        {
            get
            {
                if (!StartDate.HasValue || !EndDate.HasValue) return 0;
                var total = (EndDate.Value.Date - StartDate.Value.Date).TotalDays;
                if (total <= 0) return 100;
                var done = (DateTime.Today - StartDate.Value.Date).TotalDays;
                return (int)Math.Clamp(Math.Round(done / total * 100), 0, 100);
            }
        }

        // ── Navigation ───────────────────────────────────────────────────────────
        [ValidateNever] public ICollection<ProjectTeamMember> TeamMembers { get; set; } = new List<ProjectTeamMember>();
        [ValidateNever] public ICollection<ProjectPhase> Phases { get; set; } = new List<ProjectPhase>();
        [ValidateNever] public ICollection<WbsItem> WbsItems { get; set; } = new List<WbsItem>();
        [ValidateNever] public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
        [ValidateNever] public ICollection<Deliverable> Deliverables { get; set; } = new List<Deliverable>();
        [ValidateNever] public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
        [ValidateNever] public ICollection<ProjectRisk> Risks { get; set; } = new List<ProjectRisk>();
        [ValidateNever] public ICollection<ProjectIssue> Issues { get; set; } = new List<ProjectIssue>();
        [ValidateNever] public ICollection<ProjectChangeRequest> ChangeRequests { get; set; } = new List<ProjectChangeRequest>();
        [ValidateNever] public ICollection<ProjectDocument> Documents { get; set; } = new List<ProjectDocument>();
        [ValidateNever] public ICollection<BudgetLine> BudgetLines { get; set; } = new List<BudgetLine>();
        [ValidateNever] public ICollection<ProjectExpense> Expenses { get; set; } = new List<ProjectExpense>();
        [ValidateNever] public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
        [ValidateNever] public ICollection<ProjectAttachment> Attachments { get; set; } = new List<ProjectAttachment>();
        [ValidateNever] public ICollection<ProjectLink> Dependencies { get; set; } = new List<ProjectLink>();
    }

    /// <summary>A person assigned to a project team, with the hat they wear on it.</summary>
    public class ProjectTeamMember
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        public TeamRole Role { get; set; } = TeamRole.Member;

        /// <summary>Percentage of the person's capacity committed to this project.</summary>
        [Range(0, 100)]
        public int AllocationPercent { get; set; } = 100;

        [DataType(DataType.Date)] public DateTime? FromDate { get; set; }
        [DataType(DataType.Date)] public DateTime? ToDate { get; set; }

        /// <summary>Cleared when the member rolls off, so historical time entries stay attributable.</summary>
        public bool IsActive { get; set; } = true;

        public DateTime AddedAt { get; set; } = DateTime.Now;
    }

    /// <summary>A dependency between two projects (this project waits on another).</summary>
    public class ProjectLink
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        /// <summary>The project that must progress first.</summary>
        public int DependsOnProjectId { get; set; }
        [ValidateNever] public Project? DependsOnProject { get; set; }

        public DependencyType Type { get; set; } = DependencyType.FinishToStart;

        [StringLength(500)]
        public string? Note { get; set; }
    }

    /// <summary>A file attached to the project itself (as opposed to a controlled document).</summary>
    public class ProjectAttachment
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        [Required, StringLength(260)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>Path relative to wwwroot.</summary>
        [Required, StringLength(500)]
        public string StoredPath { get; set; } = string.Empty;

        [StringLength(120)]
        public string? ContentType { get; set; }

        public long SizeBytes { get; set; }

        public int UploadedById { get; set; }
        [ValidateNever] public User? UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Append-only audit trail for every mutation inside the project-management module —
    /// who changed what, when, from which address, and the before/after values.
    /// </summary>
    public class ProjectActivityLog
    {
        public int Id { get; set; }

        public int? ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        /// <summary>Entity type touched, e.g. "ProjectTask".</summary>
        [StringLength(80)]
        public string EntityType { get; set; } = string.Empty;

        public int? EntityId { get; set; }

        /// <summary>Verb, e.g. "Created", "StatusChanged", "Deleted".</summary>
        [StringLength(80)]
        public string Action { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Field { get; set; }

        [StringLength(1000)]
        public string? OldValue { get; set; }

        [StringLength(1000)]
        public string? NewValue { get; set; }

        [StringLength(500)]
        public string? Summary { get; set; }

        public int? UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        [StringLength(64)]
        public string? IpAddress { get; set; }

        public DateTime At { get; set; } = DateTime.Now;
    }

    /// <summary>In-app / email notification raised by the project-management module.</summary>
    public class PmNotification
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        public int? ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public PmNotificationType Type { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Message { get; set; }

        /// <summary>Relative URL the notification deep-links to.</summary>
        [StringLength(400)]
        public string? Url { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ReadAt { get; set; }

        /// <summary>Set once the email copy has been handed to the send queue.</summary>
        public bool EmailSent { get; set; }
    }
}
