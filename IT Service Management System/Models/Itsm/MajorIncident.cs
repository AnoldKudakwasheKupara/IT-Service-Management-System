using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Itsm
{
    /// <summary>
    /// ITIL Major Incident (P1/P2) — a high-impact incident run as a coordinated response with a
    /// dedicated command team. Declared from a helpdesk ticket or standalone, it tracks the response
    /// timeline, affected services/CIs, stakeholder communications, recovery/resolution and a
    /// post-incident review with follow-up actions. Distinct from the ISO investigation
    /// <see cref="IT_Service_Management_System.Models.Ims.Incident"/> record.
    /// </summary>
    public class MajorIncident
    {
        public int Id { get; set; }

        /// <summary>Human reference, e.g. "MI-00007". Stable once created.</summary>
        [NotMapped] public string Reference => $"MI-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Situation summary"), StringLength(4000)]
        public string? Summary { get; set; }

        [Display(Name = "Business impact"), StringLength(4000)]
        public string? BusinessImpact { get; set; }

        public MajorIncidentSeverity Severity { get; set; } = MajorIncidentSeverity.Sev1;
        public MajorIncidentStatus Status { get; set; } = MajorIncidentStatus.Declared;

        // ── Declaration ────────────────────────────────────────────────────────────
        [Display(Name = "Detected at")]
        public DateTime? DetectedAt { get; set; }

        [Display(Name = "Declared at")]
        public DateTime DeclaredAt { get; set; } = DateTime.Now;

        public int? DeclaredById { get; set; }
        [ValidateNever] public User? DeclaredBy { get; set; }

        /// <summary>Optional helpdesk ticket the major incident was escalated from.</summary>
        public int? SourceTicketId { get; set; }
        [ValidateNever] public Ticket? SourceTicket { get; set; }

        // ── Command team ───────────────────────────────────────────────────────────
        [Display(Name = "Incident commander")]
        public int? CommanderId { get; set; }
        [ValidateNever] public User? Commander { get; set; }

        [Display(Name = "Technical lead")]
        public int? TechnicalLeadId { get; set; }
        [ValidateNever] public User? TechnicalLead { get; set; }

        [Display(Name = "Communications lead")]
        public int? CommunicationsLeadId { get; set; }
        [ValidateNever] public User? CommunicationsLead { get; set; }

        // ── Recovery & resolution ──────────────────────────────────────────────────
        [Display(Name = "Recovery started at")]
        public DateTime? RecoveryStartedAt { get; set; }

        [Display(Name = "Workaround / temporary fix"), StringLength(4000)]
        public string? Workaround { get; set; }

        [Display(Name = "Resolved at")]
        public DateTime? ResolvedAt { get; set; }

        [Display(Name = "Resolution summary"), StringLength(4000)]
        public string? ResolutionSummary { get; set; }

        [Display(Name = "Root cause"), StringLength(4000)]
        public string? RootCauseSummary { get; set; }

        public DateTime? ClosedAt { get; set; }

        // ── Impact metrics ─────────────────────────────────────────────────────────
        [Display(Name = "Users affected"), Range(0, int.MaxValue)]
        public int? UsersAffected { get; set; }

        [Display(Name = "Downtime (minutes)"), Range(0, int.MaxValue)]
        public int? DowntimeMinutes { get; set; }

        // ── Post-incident review ───────────────────────────────────────────────────
        [Display(Name = "Review scheduled for")]
        public DateTime? ReviewScheduledAt { get; set; }

        [Display(Name = "Review held on")]
        public DateTime? ReviewHeldAt { get; set; }

        [Display(Name = "Review facilitator")]
        public int? ReviewFacilitatorId { get; set; }
        [ValidateNever] public User? ReviewFacilitator { get; set; }

        [Display(Name = "What happened"), StringLength(8000)]
        public string? PirWhatHappened { get; set; }

        [Display(Name = "What went well"), StringLength(4000)]
        public string? PirWhatWentWell { get; set; }

        [Display(Name = "What went wrong"), StringLength(4000)]
        public string? PirWhatWentWrong { get; set; }

        [Display(Name = "Lessons learned"), StringLength(4000)]
        public string? PirLessonsLearned { get; set; }

        public bool ReviewCompleted { get; set; }

        // ── Lifecycle ──────────────────────────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<MajorIncidentAffectedItem> AffectedItems { get; set; } = new List<MajorIncidentAffectedItem>();
        [ValidateNever] public ICollection<MajorIncidentTimelineEntry> Timeline { get; set; } = new List<MajorIncidentTimelineEntry>();
        [ValidateNever] public ICollection<MajorIncidentUpdate> Updates { get; set; } = new List<MajorIncidentUpdate>();
        [ValidateNever] public ICollection<MajorIncidentFollowUp> FollowUps { get; set; } = new List<MajorIncidentFollowUp>();

        [NotMapped] public bool IsOpen => Status != MajorIncidentStatus.Closed;
        [NotMapped] public bool IsResolved => Status is MajorIncidentStatus.Resolved or MajorIncidentStatus.Review or MajorIncidentStatus.Closed;

        /// <summary>Time to resolve (declaration → resolved), in minutes. Null until resolved.</summary>
        [NotMapped]
        public int? TimeToResolveMinutes =>
            ResolvedAt.HasValue ? (int)Math.Round((ResolvedAt.Value - DeclaredAt).TotalMinutes) : null;
    }

    /// <summary>A service or configuration item impacted by the major incident.</summary>
    public class MajorIncidentAffectedItem
    {
        public int Id { get; set; }
        public int MajorIncidentId { get; set; }
        [ValidateNever] public MajorIncident? MajorIncident { get; set; }

        /// <summary>Optional CMDB link. When null, <see cref="ServiceName"/> names the service free-text.</summary>
        public int? ConfigurationItemId { get; set; }
        [ValidateNever] public ConfigurationItem? ConfigurationItem { get; set; }

        [Display(Name = "Service / component"), StringLength(200)]
        public string? ServiceName { get; set; }

        [Display(Name = "Impact"), StringLength(500)]
        public string? ImpactNote { get; set; }

        /// <summary>Set once the service has been confirmed restored.</summary>
        public bool Restored { get; set; }
        public DateTime? RestoredAt { get; set; }
    }

    /// <summary>A timestamped entry in the response timeline (Section: Response timeline).</summary>
    public class MajorIncidentTimelineEntry
    {
        public int Id { get; set; }
        public int MajorIncidentId { get; set; }
        [ValidateNever] public MajorIncident? MajorIncident { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.Now;

        public MajorIncidentEventType Type { get; set; } = MajorIncidentEventType.Update;

        [Required, StringLength(2000)]
        public string Detail { get; set; } = string.Empty;

        public int? LoggedById { get; set; }
        [ValidateNever] public User? LoggedBy { get; set; }
    }

    /// <summary>A stakeholder communication issued during the incident (Section: Stakeholder updates).</summary>
    public class MajorIncidentUpdate
    {
        public int Id { get; set; }
        public int MajorIncidentId { get; set; }
        [ValidateNever] public MajorIncident? MajorIncident { get; set; }

        public DateTime PostedAt { get; set; } = DateTime.Now;

        public StakeholderChannel Channel { get; set; } = StakeholderChannel.Email;

        [Display(Name = "Audience"), StringLength(200)]
        public string? Audience { get; set; }

        [Required, StringLength(4000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>Snapshot of the incident status at the time the update was issued.</summary>
        public MajorIncidentStatus StatusAtUpdate { get; set; } = MajorIncidentStatus.Declared;

        public int? PostedById { get; set; }
        [ValidateNever] public User? PostedBy { get; set; }
    }

    /// <summary>A post-incident follow-up / corrective action (Section: Follow-up actions).</summary>
    public class MajorIncidentFollowUp
    {
        public int Id { get; set; }
        public int MajorIncidentId { get; set; }
        [ValidateNever] public MajorIncident? MajorIncident { get; set; }

        [Required, StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Owner")]
        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        [Display(Name = "Owner (external)"), StringLength(150)]
        public string? OwnerName { get; set; }

        [DataType(DataType.Date), Display(Name = "Due date")]
        public DateTime? DueDate { get; set; }

        public FollowUpStatus Status { get; set; } = FollowUpStatus.Open;
        public DateTime? CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public bool IsOverdue =>
            Status != FollowUpStatus.Done && Status != FollowUpStatus.Cancelled
            && DueDate.HasValue && DueDate.Value.Date < DateTime.Now.Date;
    }
}
