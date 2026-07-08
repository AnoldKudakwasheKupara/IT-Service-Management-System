using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    /// <summary>
    /// Reference data: a clause of an ISO standard. Used to tag documents/audits/evidence and to power
    /// the AI compliance assistant ("what evidence supports ISO 9001 Clause 8.5?").
    /// </summary>
    public class IsoClause
    {
        public int Id { get; set; }

        public IsoStandard Standard { get; set; } = IsoStandard.Iso9001;

        [Required, StringLength(20), Display(Name = "Clause")]
        public string ClauseNumber { get; set; } = string.Empty;

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [NotMapped] public string Display => $"{ClauseNumber} — {Title}";
    }

    // ── IMS notifications ────────────────────────────────────────────────────────
    public enum IsoNotificationType
    {
        DocumentPublished, AcknowledgementRequired, DocumentReviewDue, DocumentExpiring,
        CapaAssigned, CapaDue, CapaEscalated, RiskReviewDue, AuditScheduled, FindingRaised,
        TrainingExpiring, SupplierEvaluationDue, ManagementReviewScheduled, ActionAssigned, General
    }

    /// <summary>
    /// A persistent IMS notification (parallel to EFM's DocumentNotification). RecipientUserId == null
    /// targets the IMS managers group. Live delivery is via the SignalR bell; this table backs the badge count.
    /// </summary>
    public class IsoNotification
    {
        public int Id { get; set; }

        /// <summary>Null = broadcast to IMS managers (Admin / SystemsAdmin / Quality Manager).</summary>
        public int? RecipientUserId { get; set; }
        [ValidateNever] public User? Recipient { get; set; }

        public IsoNotificationType Type { get; set; } = IsoNotificationType.General;

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Url { get; set; }

        [StringLength(50)] public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
