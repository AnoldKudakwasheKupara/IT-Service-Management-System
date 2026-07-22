using System.Text;
using System.Text.Json;

namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Sends email through the SendGrid Web API (v3) using a plain HttpClient — no SDK dependency.
    /// Selected when EmailSettings:SendGridApiKey is configured.
    /// </summary>
    public class SendGridEmailSender : IEmailSender
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ConfigurationService _appConfig;
        private readonly ILogger<SendGridEmailSender> _logger;

        public SendGridEmailSender(
            IHttpClientFactory httpFactory,
            IConfiguration config,
            ConfigurationService appConfig,
            ILogger<SendGridEmailSender> logger)
        {
            _httpFactory = httpFactory;
            _config = config;
            _appConfig = appConfig;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            var apiKey = _config["EmailSettings:SendGridApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Email not sent to {Email}: SendGrid API key not configured", toEmail);
                return;
            }

            var cfg = _appConfig.Get();
            var senderEmail = cfg.SenderEmail ?? _config["EmailSettings:SenderEmail"];
            var senderName = cfg.SenderName ?? _config["EmailSettings:SenderName"] ?? "Axis IT Operations";

            if (string.IsNullOrWhiteSpace(senderEmail))
            {
                _logger.LogWarning("Email not sent to {Email}: sender email not configured", toEmail);
                return;
            }

            var payload = new
            {
                personalizations = new[]
                {
                    new { to = new[] { new { email = toEmail, name = toName } }, subject }
                },
                from = new { email = senderEmail, name = senderName },
                content = new[] { new { type = "text/html", value = htmlBody } }
            };

            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("SendGrid send to {Email} failed: {Status} {Body}", toEmail, response.StatusCode, body);
                throw new InvalidOperationException($"SendGrid returned {(int)response.StatusCode}");
            }

            _logger.LogInformation("Email sent (SendGrid) to {Email}: {Subject}", toEmail, subject);
        }
    }
}
