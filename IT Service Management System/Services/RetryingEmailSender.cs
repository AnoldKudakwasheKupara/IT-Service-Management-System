namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Decorates an <see cref="IEmailSender"/> with a small retry (3 attempts, linear backoff)
    /// to ride out transient SMTP/API hiccups. Runs on the background queue, so the wait never
    /// affects a user request.
    /// </summary>
    public class RetryingEmailSender : IEmailSender
    {
        private readonly IEmailSender _inner;
        private readonly ILogger<RetryingEmailSender> _logger;
        private const int MaxAttempts = 3;

        public RetryingEmailSender(IEmailSender inner, ILogger<RetryingEmailSender> logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await _inner.SendEmailAsync(toEmail, toName, subject, htmlBody);
                    return;
                }
                catch (Exception ex) when (attempt < MaxAttempts)
                {
                    _logger.LogWarning(ex,
                        "Email to {Email} failed (attempt {Attempt}/{Max}); retrying", toEmail, attempt, MaxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                }
            }
        }
    }
}
