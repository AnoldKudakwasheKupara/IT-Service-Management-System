namespace IT_Service_Management_System.Services.Notifications
{
    /// <summary>
    /// Drives the two scheduled email jobs: the due-date reminder scan and the daily digest.
    ///
    /// Both jobs decide for themselves whether they owe anything, and both record what they sent,
    /// so this only has to tick often enough to be timely. A half-hourly tick means the digest goes
    /// out within 30 minutes of its configured hour and a restart cannot skip a day — the next tick
    /// finds the work still outstanding. Each cycle gets its own DI scope; a failure is logged and
    /// the timer keeps running rather than taking the loop down with it.
    /// </summary>
    public class NotificationSchedulerHostedService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _clock;
        private readonly ILogger<NotificationSchedulerHostedService> _logger;

        public NotificationSchedulerHostedService(IServiceScopeFactory scopeFactory, TimeProvider clock,
            ILogger<NotificationSchedulerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _clock = clock;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let startup migrations settle before touching the database.
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { return; }

            await RunCycleAsync(stoppingToken);

            using var timer = new PeriodicTimer(Interval, _clock);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) { /* shutting down */ }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            var now = _clock.GetLocalNow().DateTime;

            // The two jobs are independent: a failing digest must not suppress reminders.
            await SafelyAsync("operational reminder scan", ct, sp =>
                sp.GetRequiredService<OperationalReminderService>().ProcessAsync(now, ct));

            await SafelyAsync("daily summary", ct, sp =>
                sp.GetRequiredService<DailySummaryService>().SendIfDueAsync(now, ct));
        }

        private async Task SafelyAsync(string what, CancellationToken ct,
            Func<IServiceProvider, Task<int>> job)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await job(scope.ServiceProvider);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled {Job} failed; will retry on the next cycle.", what);
            }
        }
    }
}
