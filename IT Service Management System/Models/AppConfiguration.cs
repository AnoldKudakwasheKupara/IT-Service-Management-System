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

        // ── Operational reminders ────────────────────────────────────────────────
        /// <summary>Comma-separated recipients for operational reminders and the daily summary.
        /// Empty falls back to every active administrator's own address.</summary>
        [StringLength(1000)]
        public string? OperationsEmailRecipients { get; set; }

        public bool NotifyOnCertificateExpiry { get; set; } = true;
        public bool NotifyOnMaintenanceDue { get; set; } = true;
        public bool NotifyOnPaymentDue { get; set; } = true;

        /// <summary>Comma-separated lead times, in days, at which a reminder is sent before the
        /// due date. One email per record per threshold, deduplicated via NotificationLog.</summary>
        [StringLength(100)]
        public string ReminderLeadDays { get; set; } = "30,14,7,1";

        /// <summary>Also chase records that are already past due, once per day.</summary>
        public bool NotifyOnOverdue { get; set; } = true;

        // ── Ticket notifications ─────────────────────────────────────────────────
        public bool NotifyOnTicketEscalation { get; set; } = true;
        /// <summary>Email the assignee and the helpdesk when a ticket trips an SLA warning or breach.</summary>
        public bool NotifyOnSlaEvent { get; set; } = true;

        // ── Daily summary ────────────────────────────────────────────────────────
        public bool DailySummaryEnabled { get; set; } = true;
        /// <summary>Hour of the day (0-23, local) at which the summary is sent.</summary>
        [Range(0, 23)]
        public int DailySummaryHour { get; set; } = 7;
        /// <summary>Also send each agent a personal digest of their own tickets and logged work.</summary>
        public bool DailySummaryPerAgent { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [StringLength(150)]
        public string? UpdatedBy { get; set; }
    }
}
