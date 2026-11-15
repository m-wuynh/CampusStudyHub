using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Pdf.Trash;

public sealed record PdfPurgeBatchResult(
    int PdfCount,
    int FileCount,
    bool HasMore);

public sealed class PdfTrashPurgeService(
    IRepository repository,
    IPdfFileStorage pdfFileStorage,
    ILogger<PdfTrashPurgeService> logger)
{
    private static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromDays(1);

    public async Task<PdfPurgeBatchResult> PurgeExpiredBatchAsync(
        DateTime utcNow,
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(utcNow);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var cutoffUtc = utcNow.AddDays(-PdfTrashPolicy.RecoveryDays);
        var activeSharedDocumentIds = repository.Query<PdfDocumentShare>()
            .Where(share => share.IsActive)
            .Select(share => share.DocumentId);

        await using var transaction = await repository.BeginTransactionAsync(
            cancellationToken);
        var documents = await repository.Query<PdfDocument>(false)
            .Where(document => document.IsDeleted &&
                               document.DeletedAtUtc <= cutoffUtc &&
                               !activeSharedDocumentIds.Contains(document.DocumentId))
            .OrderBy(document => document.DeletedAtUtc)
            .ThenBy(document => document.DocumentId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        var documentIds = documents.Select(document => document.DocumentId).ToList();

        await repository.Query<PdfAnnotation>(false)
            .Where(row => documentIds.Contains(row.DocumentId))
            .ExecuteDeleteAsync(cancellationToken);
        await repository.Query<PdfDocumentPageNote>(false)
            .Where(row => documentIds.Contains(row.DocumentId))
            .ExecuteDeleteAsync(cancellationToken);
        await repository.Query<PdfDocumentShare>(false)
            .Where(row => documentIds.Contains(row.DocumentId))
            .ExecuteDeleteAsync(cancellationToken);

        repository.RemoveRange(documents);
        await repository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var deletedFileCount = 0;
        foreach (var storageKey in documents
                     .Select(document => document.StorageKey)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await pdfFileStorage.DeleteAsync(storageKey, cancellationToken);
                deletedFileCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Không thể xóa file PDF {StorageKey}; lần quét file mồ côi sẽ thử lại.",
                    storageKey);
            }
        }

        return new PdfPurgeBatchResult(
            documents.Count,
            deletedFileCount,
            documents.Count == batchSize);
    }

    public async Task<int> DeleteOrphanPdfFilesAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(utcNow);
        var retainedStorageKeys = await repository.Query<PdfDocument>()
            .Select(document => document.StorageKey)
            .ToListAsync(cancellationToken);
        return await pdfFileStorage.DeleteOrphansAsync(
            retainedStorageKeys,
            utcNow - OrphanGracePeriod,
            cancellationToken);
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Thời điểm dọn rác Pdf phải dùng UTC.",
                nameof(value));
        }
    }
}
