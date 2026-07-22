using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class TicketMessage
    {
        public int Id { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.Now;

        /// <summary>Internal staff note — never shown to the requester and never emailed to them.</summary>
        public bool IsInternal { get; set; }

        public int TicketId { get; set; }
        public int SenderId { get; set; }

        [ValidateNever]
        public Ticket? Ticket { get; set; }

        [ValidateNever]
        public User? Sender { get; set; }

        [ValidateNever]
        public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    }
}