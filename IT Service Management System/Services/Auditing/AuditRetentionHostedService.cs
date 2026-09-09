using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Auditing
{
    /// <summary>
    /// Enforces the configured audit retention period.
    ///
    /// A purge is the one operation allowed to remove audit entries, so it records its own run —
    /// how many rows went, and the cut-off that took them — leaving the trail able to explain its
    /// own gaps. Removal is oldest-first from the head of the chain, which keeps the surviving
    /// rows' links intact; verification treats a truncated head as the new start.
    ///
    /// Runs a few minutes after startup and daily thereafter. Retention of 0 keeps everything.
    /// </summary>
    public class AuditRetentionHostedService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
        private const int BatchSize = 5_000;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditRetentionHostedService> _logger;

        public AuditRetentionHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<AuditRetentionHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let startup migrations settle before touching the database.
            try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
            catch (OperationCanceledException) { return; }

            await PurgeAsync(stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    await PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) { /* shutting down */ }
        }

        private async Task PurgeAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var config = scope.ServiceProvider.GetRequiredService<ConfigurationService>().Get();

                if (config.AuditRetentionDays <= 0) return;

                var cutoff = DateTime.Now.AddDays(-config.AuditRetentionDays);
                var total = 0;

                // Batched so a first run against a long-neglected trail cannot hold one enormous
                // transaction open.
                while (!ct.IsCancellationRequested)
                {
                    var removed = await db.AuditLogs
                        .Where(a => a.Timestamp < cutoff)
                        .OrderBy(a => a.Id)
                        .Take(BatchSize)
                        .ExecuteDeleteAsync(ct);

                    total += removed;
                    if (removed < BatchSize) break;
                }

                if (total == 0) return;

                _logger.LogInformation(
                    "Audit retention removed {Count} entries older than {Cutoff:yyyy-MM-dd}", total, cutoff);

                var writer = scope.ServiceProvider.GetRequiredService<AuditWriter>();
                var request = AuditRequestInfo.SystemContext();
                await writer.AppendAsync(db, new[]
                {
                    new AuditLog
                    {
                        UserId = request.UserId,
                        UserName = request.UserName,
                        Action = "Retention Purge",
                        Entity = nameof(AuditLog),
                        Details = $"Removed {total} audit entries older than {cutoff:yyyy-MM-dd} " +
                                  $"({config.AuditRetentionDays}-day retention)",
                        Timestamp = DateTime.Now,
                        IpAddress = request.IpAddress,
                        Device = request.Device,
                        CorrelationId = request.CorrelationId,
                        Source = AuditSource.System
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit retention purge failed");
            }
        }
    }
}
