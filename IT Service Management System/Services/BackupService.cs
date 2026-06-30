using IT_Service_Management_System.DbContexts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services
{
    public class BackupFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string SizeDisplay => SizeBytes >= 1_073_741_824
            ? $"{SizeBytes / 1_073_741_824.0:F2} GB"
            : SizeBytes >= 1_048_576 ? $"{SizeBytes / 1_048_576.0:F1} MB"
            : $"{SizeBytes / 1024.0:F0} KB";
    }

    /// <summary>
    /// On-demand SQL Server database backups with verification and retention. The automated
    /// scheduled runner is intentionally not implemented (managed via the UI / external scheduler).
    /// Backups are written by the SQL Server service account to the configured path.
    /// </summary>
    public class BackupService
    {
        private readonly ApplicationDbContext _context;
        private readonly ConfigurationService _config;
        private readonly AuditService _audit;
        private readonly AlertService _alerts;
        private readonly ILogger<BackupService> _logger;

        public BackupService(ApplicationDbContext context, ConfigurationService config,
            AuditService audit, AlertService alerts, ILogger<BackupService> logger)
        {
            _context = context;
            _config = config;
            _audit = audit;
            _alerts = alerts;
            _logger = logger;
        }

        private string DbName => _context.Database.GetDbConnection().Database;

        public string? ConfiguredPath => _config.Get().BackupPath;

        public List<BackupFileInfo> ListBackups()
        {
            var path = ConfiguredPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return new List<BackupFileInfo>();

            return Directory.GetFiles(path, "*.bak")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Select(f => new BackupFileInfo
                {
                    FileName = f.Name,
                    FullPath = f.FullName,
                    SizeBytes = f.Length,
                    CreatedAt = f.CreationTime
                })
                .ToList();
        }

        public async Task<(bool ok, string message)> BackupNowAsync()
        {
            var cfg = _config.Get();
            var path = cfg.BackupPath;

            if (string.IsNullOrWhiteSpace(path))
                return (false, "No backup folder is configured. Set one in Configuration → Backup.");

            try
            {
                Directory.CreateDirectory(path);
                var db = DbName.Replace("]", "]]");
                var file = Path.Combine(path, $"{DbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

                // Backup (WITH INIT — no compression so SQL Express is supported).
                await _context.Database.ExecuteSqlRawAsync(
                    $"BACKUP DATABASE [{db}] TO DISK = @path WITH INIT, NAME = @name;",
                    new SqlParameter("@path", file),
                    new SqlParameter("@name", $"{DbName} full backup"));

                // Verify the backup is readable/restorable.
                await _context.Database.ExecuteSqlRawAsync(
                    "RESTORE VERIFYONLY FROM DISK = @path;",
                    new SqlParameter("@path", file));

                ApplyRetention(path, cfg.BackupRetentionCount);

                await _audit.LogAsync("Backup Created", "Backup", null,
                    $"Database backup created and verified: {Path.GetFileName(file)}");

                return (true, $"Backup created and verified: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup failed");
                await _audit.LogAsync("Backup Failed", "Backup", null, ex.Message);
                await _alerts.BackupFailureAsync(ex.Message);
                return (false, $"Backup failed: {ex.Message}");
            }
        }

        public async Task<(bool ok, string message)> VerifyAsync(string fileName)
        {
            var path = ConfiguredPath;
            if (string.IsNullOrWhiteSpace(path))
                return (false, "No backup folder configured.");

            var full = Path.Combine(path, Path.GetFileName(fileName)); // guard against traversal
            if (!File.Exists(full))
                return (false, "Backup file not found.");

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "RESTORE VERIFYONLY FROM DISK = @path;", new SqlParameter("@path", full));
                await _audit.LogAsync("Backup Verified", "Backup", null, $"Verified backup {fileName}");
                return (true, $"{fileName} is valid and restorable.");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Backup Verify Failed", "Backup", null, $"{fileName}: {ex.Message}");
                await _alerts.BackupFailureAsync($"Verification failed for {fileName}: {ex.Message}");
                return (false, $"Verification failed: {ex.Message}");
            }
        }

        private void ApplyRetention(string path, int keep)
        {
            if (keep <= 0) return;
            var files = Directory.GetFiles(path, "*.bak")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Skip(keep)
                .ToList();

            foreach (var old in files)
            {
                try { old.Delete(); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete old backup {File}", old.Name); }
            }
        }
    }
}
