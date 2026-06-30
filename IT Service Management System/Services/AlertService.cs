using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;

namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Sends security alerts to administrators (per AppConfiguration toggles + recipient list)
    /// and records each as an audited "Security Alert" event.
    /// </summary>
    public class AlertService
    {
        private readonly ConfigurationService _config;
        private readonly EmailService _email;
        private readonly AuditService _audit;
        private readonly ILogger<AlertService> _logger;

        public AlertService(ConfigurationService config, EmailService email, AuditService audit, ILogger<AlertService> logger)
        {
            _config = config;
            _email = email;
            _audit = audit;
            _logger = logger;
        }

        private IEnumerable<string> Recipients(AppConfiguration cfg) =>
            (cfg.AlertEmailRecipients ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);

        private async Task DispatchAsync(string title, string severity, string intro,
            IEnumerable<KeyValuePair<string, string>> facts, string auditDetails)
        {
            var cfg = _config.Get();
            var recipients = Recipients(cfg).ToList();

            // Always record the alert in the audit trail, even if no recipients are configured.
            await _audit.LogAsync("Security Alert", "Security", null, auditDetails);

            if (recipients.Count == 0)
            {
                _logger.LogWarning("Security alert '{Title}' raised but no alert recipients are configured.", title);
                return;
            }

            var html = EmailTemplates.AdminAlert(title, severity, intro, facts);
            foreach (var to in recipients)
            {
                try { await _email.SendEmailAsync(to, "Administrator", $"[Security Alert] {title}", html); }
                catch (Exception ex) { _logger.LogError(ex, "Failed to send security alert to {To}", to); }
            }
        }

        public Task MultipleFailedLoginsAsync(string accountEmail, string ip, string device) =>
            _config.Get().AlertOnMultipleFailedLogins
                ? DispatchAsync("Account locked after multiple failed logins", "warning",
                    "An account was locked following repeated failed sign-in attempts.",
                    new Dictionary<string, string> { ["Account"] = accountEmail, ["IP address"] = ip, ["Device"] = device, ["When"] = DateTime.Now.ToString("g") },
                    $"Multiple failed logins for {accountEmail} from {ip}")
                : Task.CompletedTask;

        public Task NewAdminAccountAsync(string accountEmail, string role, string createdBy) =>
            _config.Get().AlertOnNewAdminAccount
                ? DispatchAsync("New administrator account created", "critical",
                    "A new account with administrative privileges was created.",
                    new Dictionary<string, string> { ["Account"] = accountEmail, ["Role"] = role, ["Created by"] = createdBy, ["When"] = DateTime.Now.ToString("g") },
                    $"New admin account {accountEmail} ({role}) created by {createdBy}")
                : Task.CompletedTask;

        public Task PrivilegeEscalationAsync(string accountEmail, string oldRole, string newRole, string changedBy) =>
            _config.Get().AlertOnPrivilegeEscalation
                ? DispatchAsync("Privilege escalation detected", "critical",
                    "A user account was elevated to a higher-privilege role.",
                    new Dictionary<string, string> { ["Account"] = accountEmail, ["From"] = oldRole, ["To"] = newRole, ["Changed by"] = changedBy, ["When"] = DateTime.Now.ToString("g") },
                    $"Privilege escalation: {accountEmail} {oldRole} -> {newRole} by {changedBy}")
                : Task.CompletedTask;

        public Task SuspiciousLocationAsync(string accountEmail, string location, string ip, string device) =>
            _config.Get().AlertOnSuspiciousLocation
                ? DispatchAsync("Sign-in from a new location", "warning",
                    "An account signed in from a location not seen before.",
                    new Dictionary<string, string> { ["Account"] = accountEmail, ["Location"] = location, ["IP address"] = ip, ["Device"] = device, ["When"] = DateTime.Now.ToString("g") },
                    $"Suspicious-location sign-in for {accountEmail} from {location} ({ip})")
                : Task.CompletedTask;

        public Task LargeDataExportAsync(string who, string what, int rowCount) =>
            _config.Get().AlertOnLargeDataExport
                ? DispatchAsync("Large data export", "warning",
                    "A large data export was performed.",
                    new Dictionary<string, string> { ["By"] = who, ["Export"] = what, ["Rows"] = rowCount.ToString("N0"), ["When"] = DateTime.Now.ToString("g") },
                    $"Large data export by {who}: {what} ({rowCount} rows)")
                : Task.CompletedTask;

        public Task BackupFailureAsync(string detail) =>
            _config.Get().AlertOnBackupFailure
                ? DispatchAsync("Backup failure", "critical",
                    "A database backup did not complete successfully.",
                    new Dictionary<string, string> { ["Detail"] = detail, ["When"] = DateTime.Now.ToString("g") },
                    $"Backup failure: {detail}")
                : Task.CompletedTask;

        public Task DatabaseFailureAsync(string detail) =>
            _config.Get().AlertOnDatabaseFailure
                ? DispatchAsync("Database failure", "critical",
                    "A database error occurred in the application.",
                    new Dictionary<string, string> { ["Detail"] = detail, ["When"] = DateTime.Now.ToString("g") },
                    $"Database failure: {detail}")
                : Task.CompletedTask;
    }
}
