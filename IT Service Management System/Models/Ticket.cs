using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        public string Category { get; set; }

        public TicketStatus Status { get; set; } = TicketStatus.Open;

        public TicketPriority Priority { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int CreatedById { get; set; }
        public User CreatedBy { get; set; }

        public int? AssignedToId { get; set; }
        public User AssignedTo { get; set; }

        public ICollection<TicketMessage> Messages { get; set; }
        public ICollection<TicketAttachment> Attachments { get; set; }

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
            High
        }

        public enum UserRole
        {
            Admin,
            Employee
        }
    }
}