using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Pdf.Access;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Pdf.Documents;

public sealed class PdfService(
    IRepository repository,
    ICurrentUser currentUser,
    PdfAccessService access,
    IPdfFileStorage storage,
    ILogger<PdfService> logger)
{
    public async Task<PdfDetailsDto> GetAsync(
        long documentId, long? shareId = null,
        CancellationToken ct = default)
    {
        var context = await access.RequirePdfAsync(documentId, shareId,
            cancellationToken: ct);
        return ToDto(context.Document, context.GroupId);
    }

    public async Task<Stream> OpenFileAsync(
        long documentId, long? shareId = null,
        CancellationToken ct = default)
    {
        var context = await access.RequirePdfAsync(documentId, shareId,
            cancellationToken: ct);
        return await storage.OpenReadAsync(context.Document.StorageKey, ct);
    }

    public async Task<PdfDetailsDto> UploadAsync(
        Stream content, string fileName, string? title,
        CancellationToken ct = default)
    {
        RequireAuthenticated();
        var normalizedFileName = PdfValidation.PdfFileName(fileName);
        await EnsureFileNameAvailableAsync(normalizedFileName, ct);
        var usedStorageBytes = await EnsureUploadQuotaAsync(0, ct);
        var requestedTitle = string.IsNullOrWhiteSpace(title)
            ? null
            : PdfValidation.Title(title);
        var stored = await storage.SaveAsync(content, normalizedFileName, ct);
        var committed = false;

        try
        {
            if (stored.SizeBytes > PdfPolicy.MaxStorageBytesPerUser - usedStorageBytes)
            {
                throw new PdfException(
                    409,
                    "Bạn đã sử dụng hết hạn mức 2 GiB lưu trữ PDF.");
            }

            int pageCount;
            await using (var file = await storage.OpenReadAsync(stored.StorageKey, ct))
            {
                pageCount = PdfInspection.CountPages(file, ct);
            }

            var now = DateTime.UtcNow;
            var document = new PdfDocument
            {
                OwnerUserId = currentUser.UserId,
                Title = requestedTitle ?? PdfValidation.Title(stored.FileName[..^4]),
                StorageKey = stored.StorageKey,
                OriginalFileName = stored.FileName,
                OriginalFileNameKey = stored.FileName,
                PageCount = pageCount,
                FileSizeBytes = stored.SizeBytes,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await using var transaction = await repository.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);
            // The first checks fail fast. These checks are authoritative and run
            // under the same serializable transaction as the insert.
            await EnsureFileNameAvailableAsync(stored.FileName, ct);
            await EnsureUploadQuotaAsync(stored.SizeBytes, ct);
            await repository.AddAsync(document, ct);
            await repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            return ToDto(document);
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            await TryDeleteFileAsync(stored.StorageKey);
            throw DuplicateFileName(stored.FileName);
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            await TryDeleteFileAsync(stored.StorageKey);
            throw new PdfException(
                409,
                "Có một lượt tải PDF khác đang được xử lý. Hãy thử lại.");
        }
        catch
        {
            if (!committed)
            {
                await TryDeleteFileAsync(stored.StorageKey);
            }

            throw;
        }
    }

    public async Task RenameAsync(
        long documentId, string title, string versionToken,
        CancellationToken ct = default)
    {
        var document = await GetForWriteAsync(documentId, versionToken, ct);
        document.Title = PdfValidation.Title(title);
        document.UpdatedAtUtc = DateTime.UtcNow;
        await SaveAsync(ct);
    }

    public async Task DeleteAsync(
        long documentId, string versionToken,
        CancellationToken ct = default)
    {
        try
        {
            await using var transaction = await repository.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);
            var document = await GetForWriteAsync(documentId, versionToken, ct);
            await RequireNoActiveSharesAsync(documentId, ct);
            var nowUtc = DateTime.UtcNow;
            document.IsDeleted = true;
            document.DeletedAtUtc = nowUtc;
            document.UpdatedAtUtc = nowUtc;
            await SaveAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            throw Conflict();
        }
    }

    public async Task RestoreAsync(
        long documentId, string versionToken,
        CancellationToken ct = default)
    {
        var document = await GetDeletedForWriteAsync(documentId, versionToken, ct);
        if (PdfTrashPolicy.DaysLeft(document.DeletedAtUtc, DateTime.UtcNow) == 0)
        {
            throw new PdfException(410, "Đã hết thời hạn khôi phục PDF.");
        }

        document.IsDeleted = false;
        document.DeletedAtUtc = null;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await SaveAsync(ct);
    }

    public async Task PermanentlyDeleteAsync(
        long documentId, string versionToken,
        CancellationToken ct = default)
    {
        string storageKey;
        try
        {
            await using var transaction = await repository.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);
            var document = await GetDeletedForWriteAsync(documentId, versionToken, ct);
            await RequireNoActiveSharesAsync(documentId, ct);
            storageKey = document.StorageKey;
            await DeleteDependentsAsync(documentId, ct);
            repository.Remove(document);
            await repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            throw Conflict();
        }

        await TryDeleteFileAsync(storageKey, ct);
    }

    private async Task DeleteDependentsAsync(long documentId, CancellationToken ct)
    {
        await repository.Query<PdfAnnotation>(false)
            .Where(x => x.DocumentId == documentId)
            .ExecuteDeleteAsync(ct);
        await repository.Query<PdfDocumentPageNote>(false)
            .Where(x => x.DocumentId == documentId)
            .ExecuteDeleteAsync(ct);
        await repository.Query<PdfDocumentShare>(false)
            .Where(x => x.DocumentId == documentId)
            .ExecuteDeleteAsync(ct);
    }

    private async Task RequireNoActiveSharesAsync(long documentId, CancellationToken ct)
    {
        if (await repository.Query<PdfDocumentShare>()
            .AnyAsync(x => x.DocumentId == documentId && x.IsActive, ct))
        {
            throw new PdfException(
                409,
                "PDF đang được chia sẻ trong nhóm. Hãy thu hồi tất cả lượt chia sẻ trước khi xóa.");
        }
    }

    private async Task EnsureFileNameAvailableAsync(
        string fileName,
        CancellationToken ct)
    {
        if (await repository.AnyAsync<PdfDocument>(
                document => document.OwnerUserId == currentUser.UserId &&
                            document.OriginalFileName == fileName,
                ct))
        {
            throw DuplicateFileName(fileName);
        }
    }

    private async Task<long> EnsureUploadQuotaAsync(
        long incomingBytes,
        CancellationToken ct)
    {
        var documents = repository.Query<PdfDocument>()
            .Where(document => document.OwnerUserId == currentUser.UserId);
        var documentCount = await documents.CountAsync(ct);
        if (documentCount >= PdfPolicy.MaxDocumentsPerUser)
        {
            throw new PdfException(
                409,
                $"Mỗi tài khoản được lưu tối đa {PdfPolicy.MaxDocumentsPerUser} PDF, kể cả PDF trong thùng rác.");
        }

        var usedStorageBytes = await documents.SumAsync(
            document => document.FileSizeBytes,
            ct);
        if (usedStorageBytes >= PdfPolicy.MaxStorageBytesPerUser ||
            incomingBytes > PdfPolicy.MaxStorageBytesPerUser - usedStorageBytes)
        {
            throw new PdfException(
                409,
                "Bạn đã sử dụng hết hạn mức 2 GiB lưu trữ PDF.");
        }

        return usedStorageBytes;
    }

    private async Task<PdfDocument> GetForWriteAsync(
        long documentId,
        string versionToken,
        CancellationToken ct)
    {
        RequireAuthenticated();
        var expected = PdfValidation.Version(versionToken);
        var document = await repository.Query<PdfDocument>(false)
            .SingleOrDefaultAsync(x => x.DocumentId == documentId &&
                                       x.OwnerUserId == currentUser.UserId &&
                                       !x.IsDeleted, ct)
            ?? throw Missing();
        EnsureVersion(document, expected);

        return document;
    }

    private async Task<PdfDocument> GetDeletedForWriteAsync(
        long documentId,
        string versionToken,
        CancellationToken ct)
    {
        RequireAuthenticated();
        var expected = PdfValidation.Version(versionToken);
        var document = await repository.Query<PdfDocument>(false)
            .SingleOrDefaultAsync(x => x.DocumentId == documentId &&
                                       x.OwnerUserId == currentUser.UserId &&
                                       x.IsDeleted, ct)
            ?? throw Missing();
        EnsureVersion(document, expected);
        return document;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict();
        }
    }

    private static void EnsureVersion(PdfDocument document, byte[] expected)
    {
        if (!document.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }
    }

    private static PdfDetailsDto ToDto(
        PdfDocument document,
        long? groupId = null) =>
        new(
            document.DocumentId,
            document.Title,
            document.OriginalFileName,
            document.PageCount,
            groupId,
            Convert.ToBase64String(document.RowVersion));

    private async Task TryDeleteFileAsync(
        string storageKey,
        CancellationToken ct = default)
    {
        try
        {
            await storage.DeleteAsync(storageKey, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not remove PDF file {Key}.", storageKey);
        }
    }

    private void RequireAuthenticated()
    {
        if (currentUser.UserId <= 0)
        {
            throw new PdfException(401, "Bạn cần đăng nhập.");
        }
    }

    private static PdfException Missing() =>
        new(404, "Không tìm thấy PDF.");

    private static PdfException Conflict() =>
        new(409, "PDF đã thay đổi. Hãy tải lại trước khi lưu.");

    private static bool IsUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static bool IsDeadlock(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
            {
                return true;
            }
        }

        return false;
    }

    private static PdfException DuplicateFileName(string fileName) =>
        new(
            409,
            $"Bạn đã tải lên PDF có tên '{fileName}'. Hãy đổi tên file hoặc xóa vĩnh viễn bản cũ trong thùng rác.");
}
