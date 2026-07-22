using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace IT_Service_Management_System.Services
{
    /// <summary>SMTP email sender (MailKit, StartTLS). The default <see cref="IEmailSender"/>.</summary>
    public class EmailService : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        private readonly ConfigurationService _appConfig;

        public EmailService(IConfiguration config, ILogger<EmailService> logger, ConfigurationService appConfig)
        {
            _config = config;
            _logger = logger;
            _appConfig = appConfig;
        }

        public async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            // Prefer the admin-managed configuration; fall back to appsettings. Password is
            // always read from appsettings/user-secrets and never stored in the database.
            var cfg = _appConfig.Get();
            var smtpServer = cfg.SmtpServer ?? _config["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var port = cfg.SmtpPort > 0 ? cfg.SmtpPort
                : (int.TryParse(_config["EmailSettings:Port"], out var p) ? p : 587);
            var senderEmail = cfg.SenderEmail ?? _config["EmailSettings:SenderEmail"];
            var senderPassword = _config["EmailSettings:SenderPassword"];
            var senderName = cfg.SenderName ?? _config["EmailSettings:SenderName"] ?? "Axis IT Operations";

            if (string.IsNullOrWhiteSpace(senderEmail))
            {
                _logger.LogWarning("Email not sent to {Email}: EmailSettings:SenderEmail is not configured", toEmail);
                return;
            }

            if (string.IsNullOrWhiteSpace(senderPassword))
            {
                _logger.LogWarning(
                    "Email not sent to {Email}: EmailSettings:SenderPassword is not configured. " +
                    "Run: dotnet user-secrets set \"EmailSettings:SenderPassword\" \"<app-password>\"", toEmail);
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, senderPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
                throw;
            }
        }

        // Backwards-compat overload used by older call sites that don't supply a display name.
        public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
            => SendEmailAsync(toEmail, toEmail, subject, htmlBody);
    }
}
