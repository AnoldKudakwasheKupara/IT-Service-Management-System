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

        [Phone]
        [StringLength(30)]
        public string? Phone { get; set; }

        public string? PasswordHash { get; set; }

        public string? ResetToken { get; set; }

        public DateTime? TokenExpiry { get; set; }

        public bool IsActive { get; set; } = false;

        // ── Security / account-protection fields ──────────────────────────────────
        public int FailedLoginCount { get; set; } = 0;

        /// <summary>When set and in the future, the account is locked out.</summary>
        public DateTime? LockoutEnd { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public DateTime? PasswordChangedAt { get; set; }

        /// <summary>Rotated on password change / "log out everywhere" to invalidate existing sessions.</summary>
        public string? SecurityStamp { get; set; }

        /// <summary>Per-user MFA toggle (global enable/enforcement lives in AppConfiguration).</summary>
        public bool MfaEnabled { get; set; } = false;

        /// <summary>Which second factor the user enrolled in (email OTP or an authenticator app).</summary>
        public MfaMethod MfaMethod { get; set; } = MfaMethod.None;

        /// <summary>Base32 shared secret for TOTP authenticator apps (set when enrolled in Authenticator).</summary>
        public string? TotpSecret { get; set; }

        /// <summary>JSON array of hashed one-time recovery codes (backup if the second factor is lost).</summary>
        public string? MfaRecoveryCodes { get; set; }

        /// <summary>Hashed one-time code for the current email-OTP challenge.</summary>
        public string? MfaOtpCodeHash { get; set; }
        public DateTime? MfaOtpExpiry { get; set; }

        [NotMapped]
        public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.Now;

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