using StudyHub.BLL.Services.Pdf.Trash;

namespace StudyHub.Web.BackgroundServices;

public sealed class PdfTrashBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PdfTrashBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(RunInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync(stoppingToken);
        }
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pdfCount = 0;
            var fileCount = 0;
            bool hasMore;

            do
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var purgeService = scope.ServiceProvider
                    .GetRequiredService<PdfTrashPurgeService>();
                var result = await purgeService.PurgeExpiredBatchAsync(
                    DateTime.UtcNow,
                    cancellationToken: cancellationToken);
                pdfCount += result.PdfCount;
                fileCount += result.FileCount;
                hasMore = result.HasMore;
            }
            while (hasMore);

            int orphanFileCount;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var purgeService = scope.ServiceProvider
                    .GetRequiredService<PdfTrashPurgeService>();
                orphanFileCount = await purgeService.DeleteOrphanPdfFilesAsync(
                    DateTime.UtcNow,
                    cancellationToken);
            }

            if (pdfCount > 0 || fileCount > 0 || orphanFileCount > 0)
            {
                logger.LogInformation(
                    "Đã dọn rác Pdf: {PdfCount} PDF, {FileCount} file theo metadata và " +
                    "{OrphanFileCount} file mồ côi.",
                    pdfCount,
                    fileCount,
                    orphanFileCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ứng dụng đang dừng.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Dọn rác Pdf thất bại.");
        }
    }
}
