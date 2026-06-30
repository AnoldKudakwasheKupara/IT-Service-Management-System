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
            Employee
        }
    }
}