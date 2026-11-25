using StudyHub.BLL.Services.Pdf.Documents;

namespace StudyHub.Web.BackgroundServices;

public sealed class PdfTrashBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PdfTrashBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<PdfDocumentService>();
                var deleted = await service.PurgeExpiredAsync(stoppingToken);

                if (deleted > 0)
                {
                    logger.LogInformation(
                        "Permanently deleted {Count} expired PDF documents.",
                        deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not clean the PDF trash.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
