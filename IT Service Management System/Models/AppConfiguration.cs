using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// Single-row application security configuration (Id == 1). Managed by admins via the
    /// Configuration screen. Secrets (e.g. SMTP password) are NOT stored here — they stay
    /// in user-secrets / environment variables.
    /// </summary>
    public class AppConfiguration
    {
        public int Id { get; set; }

        // ── Password policy ──────────────────────────────────────────────────────
        [Range(6, 128)]
        public int PasswordMinLength { get; set; } = 8;
        public bool PasswordRequireUppercase { get; set; } = true;
        public bool PasswordRequireLowercase { get; set; } = true;
        public bool PasswordRequireDigit { get; set; } = true;
        public bool PasswordRequireSpecial { get; set; } = true;

        /// <summary>0 = passwords never expire.</summary>
        [Range(0, 3650)]
        public int PasswordExpiryDays { get; set; } = 0;

        // ── Lockout ──────────────────────────────────────────────────────────────
        [Range(0, 50)]
        public int LockoutMaxFailedAttempts { get; set; } = 5;
        [Range(1, 1440)]
        public int LockoutDurationMinutes { get; set; } = 15;

        // ── Session ──────────────────────────────────────────────────────────────
        [Range(1, 1440)]
        public int SessionIdleTimeoutMinutes { get; set; } = 30;

        // ── MFA (email OTP) ──────────────────────────────────────────────────────
        public bool MfaEnabled { get; set; } = false;
        public bool MfaRequiredForAdmins { get; set; } = false;
        [Range(1, 60)]
        public int MfaOtpValidityMinutes { get; set; } = 10;

        // ── Email server (non-secret; password stays in user-secrets) ────────────
        [StringLength(200)]
        public string? SmtpServer { get; set; }
        public int SmtpPort { get; set; } = 587;
        [StringLength(150)]
        public string? SenderName { get; set; }
        [StringLength(200)]
        public string? SenderEmail { get; set; }

        // ── Backup ───────────────────────────────────────────────────────────────
        public bool BackupEnabled { get; set; } = false;
        /// <summary>Daily | Weekly | Monthly.</summary>
        [StringLength(20)]
        public string BackupFrequency { get; set; } = "Daily";
        /// <summary>HH:mm 24-hour.</summary>
        [StringLength(5)]
        public string BackupTime { get; set; } = "02:00";
        [Range(1, 365)]
        public int BackupRetentionCount { get; set; } = 7;
        [StringLength(400)]
        public string? BackupPath { get; set; }

        // ── Security alerts ──────────────────────────────────────────────────────
        /// <summary>Comma-separated recipient emails for admin alerts.</summary>
        [StringLength(1000)]
        public string? AlertEmailRecipients { get; set; }
        public bool AlertOnMultipleFailedLogins { get; set; } = true;
        public bool AlertOnNewAdminAccount { get; set; } = true;
        public bool AlertOnPrivilegeEscalation { get; set; } = true;
        public bool AlertOnSuspiciousLocation { get; set; } = true;
        public bool AlertOnLargeDataExport { get; set; } = true;
        public bool AlertOnBackupFailure { get; set; } = true;
        public bool AlertOnDatabaseFailure { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [StringLength(150)]
        public string? UpdatedBy { get; set; }
    }
}
