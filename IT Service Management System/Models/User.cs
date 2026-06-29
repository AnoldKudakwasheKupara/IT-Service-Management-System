using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? PasswordHash { get; set; }

        public string? ResetToken { get; set; }

        public DateTime? TokenExpiry { get; set; }

        public bool IsActive { get; set; } = false;

        [Required]
        public UserRole Role { get; set; }
        public int? DepartmentId { get; set; }

        [ValidateNever]
        public Department? Department { get; set; }

        public int? SupervisorId { get; set; }

        [ValidateNever]
        public User? Supervisor { get; set; }

        [ValidateNever]
        public ICollection<User> Subordinates { get; set; } = new List<User>();

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever]
        public ICollection<Ticket> TicketsCreated { get; set; } = new List<Ticket>();

        [ValidateNever]
        public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();

        // Assets currently assigned to this user (inverse of Asset.User).
        [ValidateNever]
        public ICollection<Asset> Assets { get; set; } = new List<Asset>();

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
}