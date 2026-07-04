namespace IT_Service_Management_System.Services.Efm
{
    /// <summary>
    /// Runs document maintenance (seed required docs, expiry alerts, retention) shortly after
    /// startup and then every 6 hours. Each run uses its own DI scope; failures are logged.
    /// </summary>
    public class DocumentMaintenanceHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DocumentMaintenanceHostedService> _logger;

        public DocumentMaintenanceHostedService(IServiceScopeFactory scopeFactory,
            ILogger<DocumentMaintenanceHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the app finish starting + migrations settle before the first run.
            try { await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken); }
            catch (OperationCanceledException) { return; }

            await RunAsync(seed: true, stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    await RunAsync(seed: false, stoppingToken);
            }
            catch (OperationCanceledException) { /* shutting down */ }
        }

        private async Task RunAsync(bool seed, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var maint = scope.ServiceProvider.GetRequiredService<DocumentMaintenanceService>();
                if (seed) await maint.SeedRequiredDocumentsAsync();
                await maint.RunExpiryScanAsync();
                await maint.RunRetentionScanAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document maintenance run failed.");
            }
        }
    }
}
