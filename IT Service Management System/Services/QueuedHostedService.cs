namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Drains the <see cref="IBackgroundTaskQueue"/>, executing each work item inside its own
    /// DI scope so failures are isolated and logged without affecting the originating request.
    /// </summary>
    public class QueuedHostedService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QueuedHostedService> _logger;

        public QueuedHostedService(
            IBackgroundTaskQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<QueuedHostedService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background task queue started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                Func<IServiceProvider, CancellationToken, Task> workItem;
                try
                {
                    workItem = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await workItem(scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A background work item failed.");
                }
            }

            _logger.LogInformation("Background task queue stopping.");
        }
    }
}
