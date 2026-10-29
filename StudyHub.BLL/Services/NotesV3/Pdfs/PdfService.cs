using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.Pdfs;

public sealed class PdfService(
    IRepository repository,
    ICurrentUser currentUser,
    INotesV3AccessService access,
    IPdfFileStorage storage,
    ILogger<PdfService> logger) : IPdfService
{
    public async Task<IReadOnlyList<PdfItem>> ListAsync(
        bool includeArchived = false,
        CancellationToken ct = default)
    {
        var versions = repository
    .Query<NotesV3DocumentVersion>();
        RequireAuthenticated();

        var documents = repository
            .Query<NotesV3Document>()
            .Where(document =>
                document.OwnerUserId == currentUser.UserId &&
                !document.IsDeleted &&
                (includeArchived || !document.IsArchived));

        var rows = await (
    from document in documents
    from version in versions
        .Where(version =>
            version.DocumentId == document.DocumentId)
        .OrderByDescending(version =>
            version.DocumentVersionId)
        .Take(1)
    orderby document.UpdatedAtUtc descending,
        document.DocumentId descending
    select new
    {
        document.DocumentId,
        version.DocumentVersionId,
        document.Title,
        version.OriginalFileName,
        version.FileSizeBytes,
        version.PageCount,
        document.IsArchived,
        document.RowVersion,
        document.UpdatedAtUtc
    })
    .ToListAsync(ct);

        return rows.Select(row => new PdfItem(
            row.DocumentId,
            row.DocumentVersionId,
            row.Title,
            row.OriginalFileName,
            row.FileSizeBytes,
            row.PageCount,
            row.IsArchived,
            Convert.ToBase64String(row.RowVersion),
            row.UpdatedAtUtc))
            .ToList();
    }

    public async Task<PdfItem> UploadAsync(
        Stream content,
        string fileName,
        string? title,
        CancellationToken ct = default)
    {
        RequireAuthenticated();

        // Kiểm tra tiêu đề người dùng trước khi ghi file.
        var requestedTitle = string.IsNullOrWhiteSpace(title)
            ? null
            : NotesV3Validation.Title(title);

        var stored = await storage.SaveAsync(
            content,
            fileName,
            ct);

        // Nếu CommitAsync lỗi do mất kết nối, kết quả commit có thể
        // chưa xác định. Không xóa file trong tình huống đó.
        var commitAttempted = false;

        try
        {
            string sha256;
            int pageCount;

            await using (var savedFile = await storage.OpenReadAsync(
                stored.StorageKey,
                ct))
            {
                pageCount = PdfInspection.ReadPageCount(savedFile, ct);

                savedFile.Position = 0;

                sha256 = Convert.ToHexString(
                    await SHA256.HashDataAsync(savedFile, ct));
            }

            var documentTitle = requestedTitle ??
                NotesV3Validation.Title(stored.FileName[..^4]);

            var now = DateTime.UtcNow;

            var document = new NotesV3Document
            {
                OwnerUserId = currentUser.UserId,
                Title = documentTitle,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await using var transaction =
                await repository.BeginTransactionAsync(ct);

            await repository.AddAsync(document, ct);
            await repository.SaveChangesAsync(ct);

            var version = new NotesV3DocumentVersion
            {
                DocumentId = document.DocumentId,
                StorageKey = stored.StorageKey,
                OriginalFileName = stored.FileName,
                Sha256 = sha256,
                FileSizeBytes = stored.SizeBytes,
                PageCount = pageCount,
                CreatedAtUtc = now
            };

            await repository.AddAsync(version, ct);
            await repository.SaveChangesAsync(ct);

            commitAttempted = true;
            await transaction.CommitAsync(ct);

            return ToItem(document, version);
        }
        catch
        {
            if (!commitAttempted)
            {
                await TryDeleteUncommittedFileAsync(
                    stored.StorageKey);
            }
            else
            {
                logger.LogWarning(
                    "PDF upload commit result is uncertain. " +
                    "Retained file {StorageKey} for reconciliation.",
                    stored.StorageKey);
            }

            throw;
        }
    }

    public async Task<PdfItem> GetAsync(
        long documentId,
        long? groupDocumentShareId = null,
        CancellationToken ct = default)
    {
        RequireAuthenticated();

        var version = await repository
            .Query<NotesV3DocumentVersion>()
            .Where(item => item.DocumentId == documentId)
            .OrderByDescending(item => item.DocumentVersionId)
            .FirstOrDefaultAsync(ct)
            ?? throw Missing();

        var document = await access.RequirePdfAsync(
            version.DocumentVersionId,
            groupDocumentShareId,
            cancellationToken: ct);

        return ToItem(document, version);
    }

    public async Task<PdfDownload> OpenFileAsync(
        long documentVersionId,
        long? groupDocumentShareId = null,
        CancellationToken ct = default)
    {
        // Phải kiểm tra quyền trước khi mở file trên máy chủ.
        await access.RequirePdfAsync(
            documentVersionId,
            groupDocumentShareId,
            cancellationToken: ct);

        var version = await repository
            .Query<NotesV3DocumentVersion>()
            .SingleOrDefaultAsync(
                item =>
                    item.DocumentVersionId == documentVersionId,
                ct)
            ?? throw Missing();

        var stream = await storage.OpenReadAsync(
            version.StorageKey,
            ct);

        // Controller trả FileStreamResult và chịu trách nhiệm
        // chuyển quyền quản lý stream cho ASP.NET Core.
        return new PdfDownload(
            stream,
            version.OriginalFileName,
            version.FileSizeBytes);
    }

    public async Task<PdfMutationResult> RenameAsync(
        long documentId,
        string title,
        string versionToken,
        CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);

        await access.RequireOwnedPdfAsync(
            documentId,
            forWrite: true,
            cancellationToken: ct);

        var document = await TrackedDocumentAsync(
            documentId,
            versionToken,
            ct);

        // Kiểm tra lại trạng thái từ bản tracking vừa đọc.
        if (document.IsArchived)
        {
            throw new NotesV3Exception(
                409,
                "Hãy bỏ lưu trữ PDF trước khi đổi tên.");
        }

        document.Title = title;
        document.UpdatedAtUtc = DateTime.UtcNow;

        await CommitAsync(ct);

        return MutationResult(document);
    }

    public async Task<PdfMutationResult> SetArchivedAsync(
        long documentId,
        bool archived,
        string versionToken,
        CancellationToken ct = default)
    {
        // Không bật forWrite vì phải cho phép bỏ lưu trữ.
        await access.RequireOwnedPdfAsync(
            documentId,
            cancellationToken: ct);

        var document = await TrackedDocumentAsync(
            documentId,
            versionToken,
            ct);

        document.IsArchived = archived;
        document.UpdatedAtUtc = DateTime.UtcNow;

        await CommitAsync(ct);

        return MutationResult(document);
    }

    private async Task<NotesV3Document> TrackedDocumentAsync(
        long documentId,
        string versionToken,
        CancellationToken ct)
    {
        var expected = NotesV3Validation.Version(versionToken);

        var document = await repository
            .Query<NotesV3Document>(asNoTracking: false)
            .SingleOrDefaultAsync(
                item => item.DocumentId == documentId &&
                        item.OwnerUserId == currentUser.UserId &&
                        !item.IsDeleted,
                ct)
            ?? throw Missing();

        if (!document.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }

        return document;
    }

    private async Task CommitAsync(CancellationToken ct)
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

    private async Task TryDeleteUncommittedFileAsync(
        string storageKey)
    {
        try
        {
            // Request có thể đã bị hủy nhưng vẫn cần dọn file.
            await storage.DeleteAsync(
                storageKey,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not clean up uncommitted PDF {StorageKey}.",
                storageKey);
        }
    }

    private void RequireAuthenticated()
    {
        if (currentUser.UserId <= 0)
        {
            throw new NotesV3Exception(
                401,
                "Bạn cần đăng nhập.");
        }
    }

    private static PdfItem ToItem(
        NotesV3Document document,
        NotesV3DocumentVersion version) =>
        new(
            document.DocumentId,
            version.DocumentVersionId,
            document.Title,
            version.OriginalFileName,
            version.FileSizeBytes,
            version.PageCount,
            document.IsArchived,
            Convert.ToBase64String(document.RowVersion),
            document.UpdatedAtUtc);

    private static PdfMutationResult MutationResult(
        NotesV3Document document) =>
        new(
            Convert.ToBase64String(document.RowVersion),
            document.UpdatedAtUtc);

    private static NotesV3Exception Missing() =>
        new(
            404,
            "Không tìm thấy PDF hoặc phiên bản PDF.");

    private static NotesV3Exception Conflict() =>
        new(
            409,
            "PDF đã thay đổi ở nơi khác. Tải lại thông tin trước khi lưu.");
}