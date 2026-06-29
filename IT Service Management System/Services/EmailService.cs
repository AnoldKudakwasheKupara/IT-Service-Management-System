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

            var smtp = new SmtpClient(_config["EmailSettings:SmtpServer"])
            {
                Port = port,
                Credentials = new NetworkCredential(
                    _config["EmailSettings:SenderEmail"],
                    _config["EmailSettings:SenderPassword"]
                ),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(_config["EmailSettings:SenderEmail"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

           await smtp.SendMailAsync(mail);
        }
    }
}