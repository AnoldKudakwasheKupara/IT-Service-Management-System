using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT_Service_Management_System.Models
{
    public class Ticket : ISoftDelete
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public TicketStatus Status { get; set; } = TicketStatus.Open;

        public TicketPriority Priority { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Last time the ticket changed (reply, status, assignment).</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>First time a staff member responded (first-response SLA).</summary>
        public DateTime? FirstRespondedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        /// <summary>SLA target resolution time, set from the applicable SLA policy at creation.</summary>
        public DateTime? DueAt { get; set; }

        /// <summary>SLA target first-response time, set from the applicable SLA policy at creation.</summary>
        public DateTime? ResponseDueAt { get; set; }

        // ── On-hold (SLA pause) &amp; escalation ────────────────────────────────────
        /// <summary>When the ticket was placed on hold (SLA paused); null when not on hold.</summary>
        public DateTime? OnHoldSince { get; set; }

        /// <summary>Accumulated minutes spent on hold, excluded from the SLA clock.</summary>
        public int PausedMinutes { get; set; }

        /// <summary>Set when the ticket has been escalated.</summary>
        public DateTime? EscalatedAt { get; set; }

        // ── ITIL links ──────────────────────────────────────────────────────────
        /// <summary>Optional link to the Problem this incident is a symptom of.</summary>
        public int? ProblemId { get; set; }
        [ValidateNever]
        public Itsm.Problem? Problem { get; set; }

        /// <summary>Optional link to the affected Configuration Item (CMDB).</summary>
        public int? ConfigurationItemId { get; set; }
        [ValidateNever]
        public Itsm.ConfigurationItem? ConfigurationItem { get; set; }

        // Customer satisfaction (CSAT), captured from the requester after resolution/closure.
        public int? SatisfactionRating { get; set; }   // 1–5
        public string? SatisfactionComment { get; set; }

        [NotMapped]
        public bool IsOpen => Status != TicketStatus.Resolved && Status != TicketStatus.Closed;

        [NotMapped]
        public bool IsOnHold => Status == TicketStatus.OnHold;

        [NotMapped]
        public bool IsEscalated => EscalatedAt.HasValue;

        /// <summary>True when the SLA target has passed and the ticket is still open. Paused while on hold.</summary>
        [NotMapped]
        public bool IsSlaBreached => DueAt.HasValue && IsOpen && !IsOnHold && DueAt.Value < DateTime.Now;

        /// <summary>True when the first-response target passed without a staff reply. Paused while on hold.</summary>
        [NotMapped]
        public bool IsResponseBreached => ResponseDueAt.HasValue && FirstRespondedAt == null
            && IsOpen && !IsOnHold && ResponseDueAt.Value < DateTime.Now;

        public int CreatedById { get; set; }
        [ValidateNever]
        public User? CreatedBy { get; set; }

        public int? AssignedToId { get; set; }
        [ValidateNever]
        public User? AssignedTo { get; set; }

        [ValidateNever]
        public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();

        [ValidateNever]
        public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();

        // Soft-delete (retained for audit/compliance; hidden by a global query filter).
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        /// <summary>Optimistic-concurrency token; prevents lost updates on concurrent edits.</summary>
        [Timestamp]
        [ValidateNever]
        public byte[]? RowVersion { get; set; }

        /// <summary>Human-friendly reference, e.g. TKT-00042.</summary>
        [NotMapped]
        public string Reference => $"TKT-{Id:D5}";

        public enum TicketStatus
        {
            Open,
            InProgress,
            OnHold,
            Resolved,
            Closed
        }

        public enum TicketPriority
        {
            Low,
            Medium,
            High,
            Critical
        }

        public enum UserRole
        {
            Admin,
            Finance,
            SystemsAdmin,
            Development,
            HR,
            Employee,
            // ── IMS / ISO roles (Integrated Management System module) ──
            QualityManager,
            GeneralManager,
            DepartmentManager,
            Auditor,
            DocumentController,
            ExternalAuditor
        }
    }
}
