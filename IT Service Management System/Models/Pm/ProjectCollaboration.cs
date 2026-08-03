using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Pm
{
    /// <summary>
    /// A controlled project document — contracts, drawings, reports, photos. Supports versioning,
    /// approval and check-out so two people cannot edit the same file at once.
    /// </summary>
    public class ProjectDocument
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public ProjectDocumentType Type { get; set; } = ProjectDocumentType.Other;
        public ProjectDocumentStatus Status { get; set; } = ProjectDocumentStatus.Draft;

        [StringLength(120)]
        public string? Category { get; set; }

        [StringLength(300)]
        public string? Tags { get; set; }

        /// <summary>Current version number; incremented each time a new file is uploaded.</summary>
        public int CurrentVersion { get; set; } = 1;

        [StringLength(260)]
        public string? FileName { get; set; }

        /// <summary>Path relative to wwwroot for the current version.</summary>
        [StringLength(500)]
        public string? StoredPath { get; set; }

        [StringLength(120)]
        public string? ContentType { get; set; }

        public long SizeBytes { get; set; }

        /// <summary>Set while the document is checked out; blocks other users from uploading.</summary>
        public int? CheckedOutById { get; set; }
        [ValidateNever] public User? CheckedOutBy { get; set; }
        public DateTime? CheckedOutAt { get; set; }

        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        /// <summary>Whether the client can see and download this document in the portal.</summary>
        public bool VisibleToClient { get; set; }

        public int UploadedById { get; set; }
        [ValidateNever] public User? UploadedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        [NotMapped]
        public bool IsCheckedOut => CheckedOutById.HasValue;

        [ValidateNever] public ICollection<ProjectDocumentVersion> Versions { get; set; } = new List<ProjectDocumentVersion>();
    }

    /// <summary>A superseded revision of a project document, retained for the audit trail.</summary>
    public class ProjectDocumentVersion
    {
        public int Id { get; set; }

        public int DocumentId { get; set; }
        [ValidateNever] public ProjectDocument? Document { get; set; }

        public int VersionNumber { get; set; }

        [StringLength(260)] public string? FileName { get; set; }
        [StringLength(500)] public string? StoredPath { get; set; }
        [StringLength(120)] public string? ContentType { get; set; }
        public long SizeBytes { get; set; }

        [StringLength(1000)]
        public string? ChangeNote { get; set; }

        public int UploadedById { get; set; }
        [ValidateNever] public User? UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// A project meeting with its agenda, attendance, minutes, decisions and follow-up actions.
    /// Mirrors the operations Meeting Minutes module but scoped to a project.
    /// </summary>
    public class ProjectMeeting
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Agenda { get; set; }

        public DateTime ScheduledAt { get; set; } = DateTime.Now;

        public int DurationMinutes { get; set; } = 60;

        [StringLength(250)]
        public string? Location { get; set; }

        /// <summary>Video-call link for remote attendees.</summary>
        [StringLength(500)]
        public string? MeetingLink { get; set; }

        public ProjectMeetingStatus Status { get; set; } = ProjectMeetingStatus.Scheduled;

        [StringLength(8000)]
        public string? Minutes { get; set; }

        [StringLength(4000)]
        public string? Decisions { get; set; }

        public int OrganiserId { get; set; }
        [ValidateNever] public User? Organiser { get; set; }

        /// <summary>Set when a reminder has been queued, so it is only sent once.</summary>
        public bool ReminderSent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<ProjectMeetingAttendee> Attendees { get; set; } = new List<ProjectMeetingAttendee>();
        [ValidateNever] public ICollection<ProjectMeetingAction> Actions { get; set; } = new List<ProjectMeetingAction>();
    }

    /// <summary>An invitee to a project meeting and their attendance state.</summary>
    public class ProjectMeetingAttendee
    {
        public int Id { get; set; }

        public int MeetingId { get; set; }
        [ValidateNever] public ProjectMeeting? Meeting { get; set; }

        public int UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        public AttendanceState State { get; set; } = AttendanceState.Invited;

        [StringLength(500)]
        public string? Note { get; set; }
    }

    /// <summary>An action item captured in the minutes, optionally promoted into a project task.</summary>
    public class ProjectMeetingAction
    {
        public int Id { get; set; }

        public int MeetingId { get; set; }
        [ValidateNever] public ProjectMeeting? Meeting { get; set; }

        [Required, StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        [DataType(DataType.Date)] public DateTime? DueDate { get; set; }

        public bool IsDone { get; set; }
        public DateTime? CompletedAt { get; set; }

        /// <summary>Set when the action was turned into a tracked project task.</summary>
        public int? LinkedTaskId { get; set; }
        [ValidateNever] public ProjectTask? LinkedTask { get; set; }
    }

    /// <summary>
    /// A message in a project discussion thread — the module's internal chat. Announcements are
    /// pinned messages flagged <see cref="IsAnnouncement"/>.
    /// </summary>
    public class ProjectDiscussion
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        /// <summary>Null for a top-level post; set for a reply.</summary>
        public int? ParentId { get; set; }
        [ValidateNever] public ProjectDiscussion? Parent { get; set; }

        [StringLength(250)]
        public string? Subject { get; set; }

        [Required, StringLength(8000)]
        public string Body { get; set; } = string.Empty;

        public int AuthorId { get; set; }
        [ValidateNever] public User? Author { get; set; }

        /// <summary>Pinned to the top of the project feed and pushed to every team member.</summary>
        public bool IsAnnouncement { get; set; }

        /// <summary>Visible to the client in the portal.</summary>
        public bool VisibleToClient { get; set; }

        [StringLength(300)]
        public string? MentionedUserIds { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; }
    }

    /// <summary>A reusable project blueprint — phases, milestones and a starter task list.</summary>
    public class ProjectTemplate
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public ProjectCategory Category { get; set; } = ProjectCategory.Other;
        public ProjectType Type { get; set; } = ProjectType.Internal;

        /// <summary>Typical duration, used to pre-fill the end date when instantiating.</summary>
        public int DefaultDurationDays { get; set; } = 90;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultBudget { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Built-in templates shipped with the module; cannot be deleted.</summary>
        public bool IsSystem { get; set; }

        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<ProjectTemplateItem> Items { get; set; } = new List<ProjectTemplateItem>();
    }

    /// <summary>
    /// One entry in a template — a phase, milestone or task. Dates are expressed as day offsets
    /// from the project start so the blueprint works whenever it is instantiated.
    /// </summary>
    public class ProjectTemplateItem
    {
        public int Id { get; set; }

        public int TemplateId { get; set; }
        [ValidateNever] public ProjectTemplate? Template { get; set; }

        /// <summary>"Phase", "Milestone" or "Task".</summary>
        [Required, StringLength(20)]
        public string ItemType { get; set; } = "Task";

        [Required, StringLength(250)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public int Sequence { get; set; }

        /// <summary>Days after the project start date on which this item begins.</summary>
        public int StartOffsetDays { get; set; }

        public int DurationDays { get; set; } = 1;

        [Column(TypeName = "decimal(10,2)")]
        public decimal EstimatedHours { get; set; }

        /// <summary>Sequence of the phase item this belongs to, so the hierarchy survives instantiation.</summary>
        public int? ParentSequence { get; set; }
    }

    /// <summary>
    /// The project closure record — deliverable sign-off, client acceptance, the final financial
    /// position, resource release and the post-implementation review.
    /// </summary>
    public class ProjectClosure
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public ClosureStatus Status { get; set; } = ClosureStatus.NotStarted;

        [StringLength(4000)]
        [Display(Name = "Outstanding issues")]
        public string? OutstandingIssues { get; set; }

        [StringLength(4000)]
        [Display(Name = "Post-implementation review")]
        public string? PostImplementationReview { get; set; }

        // ── Client acceptance ────────────────────────────────────────────────────
        public bool ClientAccepted { get; set; }
        [StringLength(200)] public string? ClientAcceptedBy { get; set; }
        [DataType(DataType.Date)] public DateTime? ClientAcceptedDate { get; set; }
        [StringLength(2000)] public string? ClientAcceptanceNotes { get; set; }

        // ── Final financial summary (snapshotted at closure) ─────────────────────
        [Column(TypeName = "decimal(18,2)")] public decimal FinalBudget { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal FinalActualSpend { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal FinalActualHours { get; set; }

        // ── Wrap-up checklist ────────────────────────────────────────────────────
        public bool DeliverablesSignedOff { get; set; }
        public bool ResourcesReleased { get; set; }
        public bool AssetsReturned { get; set; }
        public bool DocumentationArchived { get; set; }
        public bool FinancesReconciled { get; set; }

        public int? ClosedById { get; set; }
        [ValidateNever] public User? ClosedBy { get; set; }
        public DateTime? ClosedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Final cost variance — positive means the project came in under budget.</summary>
        [NotMapped]
        public decimal BudgetVariance => FinalBudget - FinalActualSpend;

        [NotMapped]
        public int ChecklistCompletePercent
        {
            get
            {
                var flags = new[] { DeliverablesSignedOff, ResourcesReleased, AssetsReturned, DocumentationArchived, FinancesReconciled, ClientAccepted };
                return (int)Math.Round(flags.Count(f => f) * 100.0 / flags.Length);
            }
        }
    }

    /// <summary>A lesson captured during or at the end of a project, for reuse on future work.</summary>
    public class LessonLearned
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        public LessonCategory Category { get; set; } = LessonCategory.WhatWentWell;

        [StringLength(120)]
        public string? Area { get; set; }

        [StringLength(2000)]
        public string? Recommendation { get; set; }

        public int RaisedById { get; set; }
        [ValidateNever] public User? RaisedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
