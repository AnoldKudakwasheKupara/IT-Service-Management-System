using System.ComponentModel.DataAnnotations;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public UserRole Role { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Ticket> TicketsCreated { get; set; }
        public ICollection<TicketMessage> Messages { get; set; }
    }
}