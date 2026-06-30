using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Loads and persists the single-row <see cref="AppConfiguration"/>, with in-memory caching.
    /// Falls back to seeded defaults (from appsettings where relevant) the first time it runs.
    /// </summary>
    public class ConfigurationService
    {
        private const string CacheKey = "AppConfiguration";
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _appConfig;

        public ConfigurationService(ApplicationDbContext context, IMemoryCache cache, IConfiguration appConfig)
        {
            _context = context;
            _cache = cache;
            _appConfig = appConfig;
        }

        /// <summary>Returns the current configuration, creating a default row on first use.</summary>
        public AppConfiguration Get()
        {
            if (_cache.TryGetValue(CacheKey, out AppConfiguration? cached) && cached != null)
                return cached;

            var config = _context.AppConfigurations.AsNoTracking().FirstOrDefault();
            if (config == null)
            {
                config = CreateDefault();
                _context.AppConfigurations.Add(config);
                _context.SaveChanges();
            }

            _cache.Set(CacheKey, config, TimeSpan.FromMinutes(30));
            return config;
        }

        /// <summary>Persists changes to the configuration row and refreshes the cache.</summary>
        public async Task SaveAsync(AppConfiguration updated, string? updatedBy)
        {
            var existing = await _context.AppConfigurations.FirstOrDefaultAsync();
            if (existing == null)
            {
                existing = CreateDefault();
                _context.AppConfigurations.Add(existing);
            }

            // Password policy
            existing.PasswordMinLength = updated.PasswordMinLength;
            existing.PasswordRequireUppercase = updated.PasswordRequireUppercase;
            existing.PasswordRequireLowercase = updated.PasswordRequireLowercase;
            existing.PasswordRequireDigit = updated.PasswordRequireDigit;
            existing.PasswordRequireSpecial = updated.PasswordRequireSpecial;
            existing.PasswordExpiryDays = updated.PasswordExpiryDays;

            // Lockout
            existing.LockoutMaxFailedAttempts = updated.LockoutMaxFailedAttempts;
            existing.LockoutDurationMinutes = updated.LockoutDurationMinutes;

            // Session
            existing.SessionIdleTimeoutMinutes = updated.SessionIdleTimeoutMinutes;

            // MFA
            existing.MfaEnabled = updated.MfaEnabled;
            existing.MfaRequiredForAdmins = updated.MfaRequiredForAdmins;
            existing.MfaOtpValidityMinutes = updated.MfaOtpValidityMinutes;

            // Email server (non-secret)
            existing.SmtpServer = updated.SmtpServer;
            existing.SmtpPort = updated.SmtpPort;
            existing.SenderName = updated.SenderName;
            existing.SenderEmail = updated.SenderEmail;

            // Backup
            existing.BackupEnabled = updated.BackupEnabled;
            existing.BackupFrequency = updated.BackupFrequency;
            existing.BackupTime = updated.BackupTime;
            existing.BackupRetentionCount = updated.BackupRetentionCount;
            existing.BackupPath = updated.BackupPath;

            // Alerts
            existing.AlertEmailRecipients = updated.AlertEmailRecipients;
            existing.AlertOnMultipleFailedLogins = updated.AlertOnMultipleFailedLogins;
            existing.AlertOnNewAdminAccount = updated.AlertOnNewAdminAccount;
            existing.AlertOnPrivilegeEscalation = updated.AlertOnPrivilegeEscalation;
            existing.AlertOnSuspiciousLocation = updated.AlertOnSuspiciousLocation;
            existing.AlertOnLargeDataExport = updated.AlertOnLargeDataExport;
            existing.AlertOnBackupFailure = updated.AlertOnBackupFailure;
            existing.AlertOnDatabaseFailure = updated.AlertOnDatabaseFailure;

            existing.UpdatedAt = DateTime.Now;
            existing.UpdatedBy = updatedBy;

            await _context.SaveChangesAsync();
            _cache.Remove(CacheKey);
        }

        private AppConfiguration CreateDefault() => new()
        {
            // Seed email-server fields from appsettings so the screen isn't blank on first load.
            SmtpServer = _appConfig["EmailSettings:SmtpServer"] ?? "smtp.gmail.com",
            SmtpPort = int.TryParse(_appConfig["EmailSettings:Port"], out var p) ? p : 587,
            SenderName = _appConfig["EmailSettings:SenderName"] ?? "Axis IT Operations",
            SenderEmail = _appConfig["EmailSettings:SenderEmail"],
            UpdatedAt = DateTime.Now
        };
    }
}
