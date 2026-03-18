using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class TicketMessage
    {
        public int Id { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;

        // Foreign Keys
        public int TicketId { get; set; }
        public Ticket Ticket { get; set; }

        public int SenderId { get; set; }
        public User Sender { get; set; }

        // Navigation
        public ICollection<TicketAttachment> Attachments { get; set; }
    }
}