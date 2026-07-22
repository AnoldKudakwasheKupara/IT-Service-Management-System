namespace IT_Service_Management_System.Services
{
    /// <summary>Transport-agnostic email sender. Implemented by SMTP (MailKit) and SendGrid.</summary>
    public interface IEmailSender
    {
        Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);
    }
}
