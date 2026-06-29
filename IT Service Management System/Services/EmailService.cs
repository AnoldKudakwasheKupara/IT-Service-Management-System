using System.Net;
using System.Net.Mail;

namespace IT_Service_Management_System.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var port = int.TryParse(_config["EmailSettings:Port"], out var p) ? p : 587;

            var senderEmail = _config["EmailSettings:SenderEmail"]
                ?? throw new InvalidOperationException("EmailSettings:SenderEmail is not configured.");

            var smtp = new SmtpClient(_config["EmailSettings:SmtpServer"])
            {
                Port = port,
                Credentials = new NetworkCredential(
                    senderEmail,
                    _config["EmailSettings:SenderPassword"]
                ),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(senderEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

           await smtp.SendMailAsync(mail);
        }
    }
}