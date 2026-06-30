using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// A persistent record of a logged-in session. Lets the system list active sessions,
    /// detect concurrent logins, and revoke sessions ("log out from all devices",
    /// invalidate after password change).
    /// </summary>
    public class UserSession
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [ValidateNever]
        public User? User { get; set; }

        /// <summary>Opaque token also stored in the ASP.NET session; matched on each request.</summary>
        public string SessionToken { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Device { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastSeenAt { get; set; } = DateTime.Now;

        /// <summary>Set when the session is explicitly ended (logout, revoke, password change).</summary>
        public DateTime? RevokedAt { get; set; }

        public string? RevokedReason { get; set; }

        [NotMapped]
        public bool IsActive => RevokedAt == null;
    }
}
