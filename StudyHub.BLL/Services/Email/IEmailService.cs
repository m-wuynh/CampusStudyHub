namespace StudyHub.BLL.Services.Email;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}
