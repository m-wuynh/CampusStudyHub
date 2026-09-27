using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyHub.BLL.Services.Email;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Entities;

namespace StudyHub.Web.BackgroundServices;

public class EmailNotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailNotificationBackgroundService> _logger;

    public EmailNotificationBackgroundService(IServiceProvider serviceProvider, ILogger<EmailNotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Notification Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEmailRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing email reminders.");
            }

            // Check every 10 seconds
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("Email Notification Background Service is stopping.");
    }

    private async Task ProcessEmailRemindersAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StudyHubDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var utcNow = DateTime.UtcNow;

        var pendingEmails = await context.Reminders
            .Include(r => r.ScheduleEvent)
            .Include(r => r.User)
            .Where(r => r.Channel == "Email" && r.Status == "Pending" && r.RemindAtUtc <= utcNow)
            .ToListAsync(stoppingToken);

        if (!pendingEmails.Any()) return;

        foreach (var reminder in pendingEmails)
        {
            var userEmail = reminder.User?.Email;
            if (string.IsNullOrEmpty(userEmail))
            {
                reminder.Status = "Failed";
                continue;
            }

            var ev = reminder.ScheduleEvent;
            if (ev == null)
            {
                reminder.Status = "Failed";
                continue;
            }

            var subject = $"Thông báo sự kiện: {ev.Title}";
            var htmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
                    <div style='background-color: #4f46e5; padding: 20px; color: white;'>
                        <h2 style='margin: 0;'>Bạn có một sự kiện sắp diễn ra!</h2>
                    </div>
                    <div style='padding: 20px;'>
                        <h3 style='color: #1e293b; margin-top: 0;'>{ev.Title}</h3>
                        <p style='color: #475569; margin: 5px 0;'><strong>Thời gian:</strong> {ev.StartAtLocal:dd/MM/yyyy HH:mm} - {ev.EndAtLocal:HH:mm}</p>
                        {(string.IsNullOrEmpty(ev.Location) ? "" : $"<p style='color: #475569; margin: 5px 0;'><strong>Địa điểm:</strong> {ev.Location}</p>")}
                        {(string.IsNullOrEmpty(ev.Description) ? "" : $"<p style='color: #475569; margin: 5px 0;'><strong>Mô tả:</strong> {ev.Description}</p>")}
                        <p style='color: #475569; margin-top: 20px;'>Đây là lời nhắc tự động từ Study Hub Calendar.</p>
                    </div>
                </div>";

            try
            {
                await emailService.SendEmailAsync(userEmail, subject, htmlBody);
                reminder.Status = "Sent";
                reminder.SentAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email for Reminder {ReminderId}", reminder.ReminderId);
                // Optionally retry later, but for now mark as failed to avoid infinite loop
                reminder.Status = "Failed"; 
            }
        }

        await context.SaveChangesAsync(stoppingToken);
    }
}
