using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class TicketAttachment
    {
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; }

        [Required]
        public string FilePath { get; set; }

        public string ContentType { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        public int? TicketId { get; set; }

        public int? TicketMessageId { get; set; }
        [ValidateNever]
        public Ticket Ticket { get; set; }

        [ValidateNever]
        public TicketMessage TicketMessage { get; set; }
    }
}