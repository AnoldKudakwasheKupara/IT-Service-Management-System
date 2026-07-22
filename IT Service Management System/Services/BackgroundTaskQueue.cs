using System.Threading.Channels;

namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// A simple in-process background work queue. Work items receive a fresh DI scope when run,
    /// so they can resolve scoped services (DbContext, EmailService, …) independent of any request.
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem);
        ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }

    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue =
            Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();

        public void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem)
        {
            ArgumentNullException.ThrowIfNull(workItem);
            _queue.Writer.TryWrite(workItem);
        }

        public ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
            => _queue.Reader.ReadAsync(cancellationToken);
    }
}
