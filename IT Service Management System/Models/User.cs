using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
        public string? PasswordHash { get; set; }

        public string? ResetToken { get; set; }
        public DateTime? TokenExpiry { get; set; }
        public bool IsActive { get; set; } = false;

        [Required]
        public UserRole Role { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever]
        public ICollection<Ticket> TicketsCreated { get; set; }

        [ValidateNever]
        public ICollection<TicketMessage> Messages { get; set; }
    }
}