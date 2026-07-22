namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Queues outbound email so the HTTP request returns immediately. The send runs on the
    /// background queue in its own DI scope, so a slow/failing SMTP server never blocks or
    /// breaks a user action. Failures are logged by the queue, not surfaced to the request.
    /// </summary>
    public class EmailDispatcher
    {
        private readonly IBackgroundTaskQueue _queue;

        public EmailDispatcher(IBackgroundTaskQueue queue) => _queue = queue;

        public void Queue(string toEmail, string toName, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return;

            _queue.Enqueue(async (sp, ct) =>
            {
                var email = sp.GetRequiredService<IEmailSender>();
                await email.SendEmailAsync(toEmail, toName, subject, htmlBody);
            });
        }
    }
}
