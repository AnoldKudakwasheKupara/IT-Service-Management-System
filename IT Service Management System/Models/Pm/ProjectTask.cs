using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Pm
{
    /// <summary>
    /// A unit of work inside a project. Tasks nest arbitrarily deep (parent/subtask), carry their
    /// own schedule and effort figures, and drive both the Kanban board and the Gantt chart.
    /// </summary>
    public class ProjectTask
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? ParentTaskId { get; set; }
        [ValidateNever] public ProjectTask? ParentTask { get; set; }
        [ValidateNever] public ICollection<ProjectTask> Subtasks { get; set; } = new List<ProjectTask>();

        public int? WbsItemId { get; set; }
        [ValidateNever] public WbsItem? WbsItem { get; set; }

        public int? PhaseId { get; set; }
        [ValidateNever] public ProjectPhase? Phase { get; set; }

        public int? MilestoneId { get; set; }
        [ValidateNever] public Milestone? Milestone { get; set; }

        [Required, StringLength(250)]
        [Display(Name = "Task name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public int? AssignedToId { get; set; }
        [ValidateNever] public User? AssignedTo { get; set; }

        /// <summary>Who signs the work off when it moves to Under Review.</summary>
        public int? ReviewerId { get; set; }
        [ValidateNever] public User? Reviewer { get; set; }

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.NotStarted;

        /// <summary>Board lane. Kept separate from Status so the board can be re-arranged freely.</summary>
        public KanbanColumn Column { get; set; } = KanbanColumn.Backlog;

        /// <summary>Manual sort position within the Kanban column.</summary>
        public int BoardOrder { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal EstimatedHours { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal ActualHours { get; set; }

        [DataType(DataType.Date)] public DateTime? StartDate { get; set; }
        [DataType(DataType.Date)] public DateTime? DueDate { get; set; }
        [DataType(DataType.Date)] public DateTime? CompletionDate { get; set; }

        /// <summary>Baseline dates captured when the plan is frozen — the Gantt baseline bar.</summary>
        [DataType(DataType.Date)] public DateTime? BaselineStartDate { get; set; }
        [DataType(DataType.Date)] public DateTime? BaselineDueDate { get; set; }

        [Range(0, 100)]
        public int PercentComplete { get; set; }

        /// <summary>Marks the task as lying on the computed critical path (refreshed by the scheduler).</summary>
        public bool IsOnCriticalPath { get; set; }

        /// <summary>Slack in days before this task starts delaying the project finish.</summary>
        public int FloatDays { get; set; }

        /// <summary>Whether hours booked against this task can be on-charged to the client.</summary>
        public bool IsBillable { get; set; }

        [StringLength(300)]
        public string? Tags { get; set; }

        /// <summary>Why the task is blocked, shown prominently on the board.</summary>
        [StringLength(1000)]
        public string? BlockedReason { get; set; }

        public int CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        // ── Derived ──────────────────────────────────────────────────────────────
        [NotMapped]
        public string Reference => $"TSK-{Id:D5}";

        [NotMapped]
        public bool IsOpen => Status is not (ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled);

        [NotMapped]
        public bool IsOverdue => IsOpen && DueDate.HasValue && DueDate.Value.Date < DateTime.Today;

        /// <summary>Hours booked beyond the estimate (0 when within budget).</summary>
        [NotMapped]
        public decimal HoursVariance => ActualHours - EstimatedHours;

        // ── Navigation ───────────────────────────────────────────────────────────
        [ValidateNever] public ICollection<TaskChecklistItem> Checklist { get; set; } = new List<TaskChecklistItem>();
        [ValidateNever] public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
        [ValidateNever] public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
        [ValidateNever] public ICollection<TaskDependency> Dependencies { get; set; } = new List<TaskDependency>();
        [ValidateNever] public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    }

    /// <summary>A predecessor relationship between two tasks, used by the Gantt and critical-path pass.</summary>
    public class TaskDependency
    {
        public int Id { get; set; }

        /// <summary>The dependent (successor) task.</summary>
        public int TaskId { get; set; }
        [ValidateNever] public ProjectTask? Task { get; set; }

        /// <summary>The task that must happen first.</summary>
        public int PredecessorTaskId { get; set; }
        [ValidateNever] public ProjectTask? PredecessorTask { get; set; }

        public DependencyType Type { get; set; } = DependencyType.FinishToStart;

        /// <summary>Delay (positive) or overlap (negative) in days between the two tasks.</summary>
        public int LagDays { get; set; }
    }

    /// <summary>One tick-box on a task's checklist.</summary>
    public class TaskChecklistItem
    {
        public int Id { get; set; }

        public int TaskId { get; set; }
        [ValidateNever] public ProjectTask? Task { get; set; }

        [Required, StringLength(300)]
        public string Text { get; set; } = string.Empty;

        public bool IsDone { get; set; }
        public int Sequence { get; set; }

        public DateTime? CompletedAt { get; set; }
        public int? CompletedById { get; set; }
        [ValidateNever] public User? CompletedBy { get; set; }
    }

    /// <summary>A comment on a task. Supports @mentions, which raise notifications.</summary>
    public class TaskComment
    {
        public int Id { get; set; }

        public int TaskId { get; set; }
        [ValidateNever] public ProjectTask? Task { get; set; }

        [Required, StringLength(4000)]
        public string Body { get; set; } = string.Empty;

        public int AuthorId { get; set; }
        [ValidateNever] public User? Author { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? EditedAt { get; set; }

        /// <summary>Comma-separated user ids mentioned in the body, parsed on save.</summary>
        [StringLength(300)]
        public string? MentionedUserIds { get; set; }
    }

    /// <summary>A file attached to a task.</summary>
    public class TaskAttachment
    {
        public int Id { get; set; }

        public int TaskId { get; set; }
        [ValidateNever] public ProjectTask? Task { get; set; }

        [Required, StringLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string StoredPath { get; set; } = string.Empty;

        [StringLength(120)]
        public string? ContentType { get; set; }

        public long SizeBytes { get; set; }

        public int UploadedById { get; set; }
        [ValidateNever] public User? UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}
