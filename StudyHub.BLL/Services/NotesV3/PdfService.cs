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
    public async Task<PdfDetailsDto> GetAsync(
        long documentId, long? shareId = null,
        CancellationToken ct = default)
    {
        var version = await repository.Query<NotesV3DocumentVersion>()
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.DocumentVersionId)
            .FirstOrDefaultAsync(ct)
            ?? throw Missing();

        var document = await access.RequirePdfAsync(
            version.DocumentVersionId, shareId, cancellationToken: ct);

        return ToDto(document, version);
    }

    public async Task<PdfDownloadDto> OpenFileAsync(
        long versionId, long? shareId = null,
        CancellationToken ct = default)
    {
        await access.RequirePdfAsync(versionId, shareId, cancellationToken: ct);

        var version = await repository.Query<NotesV3DocumentVersion>()
            .SingleOrDefaultAsync(x => x.DocumentVersionId == versionId, ct)
            ?? throw Missing();

        var stream = await storage.OpenReadAsync(version.StorageKey, ct);
        return new PdfDownloadDto(stream, version.OriginalFileName);
    }

    public async Task<PdfDetailsDto> UploadAsync(
        Stream content, string fileName, string? title,
        CancellationToken ct = default)
    {
        if (currentUser.UserId <= 0)
        {
            throw new NotesV3Exception(401, "Bạn cần đăng nhập.");
        }

        var requestedTitle = string.IsNullOrWhiteSpace(title)
            ? null
            : NotesV3Validation.Title(title);

        var stored = await storage.SaveAsync(content, fileName, ct);
        var commitAttempted = false;

        try
        {
            int pageCount;
            string sha256;

            await using (var file = await storage.OpenReadAsync(stored.StorageKey, ct))
            {
                pageCount = PdfInspection.ReadPageCount(file, ct);
                file.Position = 0;
                sha256 = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            }

            var now = DateTime.UtcNow;
            var document = new NotesV3Document
            {
                OwnerUserId = currentUser.UserId,
                Title = requestedTitle ?? NotesV3Validation.Title(stored.FileName[..^4]),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await using var transaction = await repository.BeginTransactionAsync(ct);

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
            return ToDto(document, version);
        }
        catch
        {
            if (!commitAttempted)
            {
                try
                {
                    await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not remove uncommitted PDF {Key}.",
                        stored.StorageKey);
                }
            }

            throw;
        }
    }

    public async Task RenameAsync(
        long documentId, string title, string versionToken,
        CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        await access.RequireOwnedPdfAsync(documentId, forWrite: true,
            cancellationToken: ct);

        var document = await GetForWriteAsync(documentId, versionToken, ct);
        document.Title = title;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await SaveAsync(ct);
    }

    public async Task SetArchivedAsync(
        long documentId, bool archived, string versionToken,
        CancellationToken ct = default)
    {
        // Bỏ lưu trữ cũng phải được phép, nên không dùng forWrite ở đây.
        await access.RequireOwnedPdfAsync(documentId, cancellationToken: ct);

        var document = await GetForWriteAsync(documentId, versionToken, ct);
        document.IsArchived = archived;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await SaveAsync(ct);
    }

    public async Task DeleteAsync(
        long documentId, string versionToken,
        CancellationToken ct = default)
    {
        await access.RequireOwnedPdfAsync(documentId, cancellationToken: ct);

        var document = await GetForWriteAsync(documentId, versionToken, ct);
        var nowUtc = DateTime.UtcNow;
        document.IsDeleted = true;
        document.DeletedAtUtc = nowUtc;
        document.UpdatedAtUtc = nowUtc;
        await SaveAsync(ct);
    }

    public async Task RestoreAsync(
        long documentId, string versionToken,
        CancellationToken ct = default)
    {
        var expected = NotesV3Validation.Version(versionToken);
        var document = await repository.Query<NotesV3Document>(false)
            .SingleOrDefaultAsync(x => x.DocumentId == documentId &&
                                       x.OwnerUserId == currentUser.UserId &&
                                       x.IsDeleted, ct)
            ?? throw Missing();

        if (!document.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }

        if (NotesTrashPolicy.DaysLeft(document.DeletedAtUtc, DateTime.UtcNow) == 0)
        {
            throw new NotesV3Exception(410, "Đã hết thời hạn khôi phục PDF.");
        }

        document.IsDeleted = false;
        document.DeletedAtUtc = null;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await SaveAsync(ct);
    }

    private async Task<NotesV3Document> GetForWriteAsync(
        long documentId, string versionToken, CancellationToken ct)
    {
        var expected = NotesV3Validation.Version(versionToken);
        var document = await repository.Query<NotesV3Document>(false)
            .SingleOrDefaultAsync(x => x.DocumentId == documentId &&
                                       x.OwnerUserId == currentUser.UserId &&
                                       !x.IsDeleted, ct)
            ?? throw Missing();

        if (!document.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }

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

    private static PdfDetailsDto ToDto(
        NotesV3Document document, NotesV3DocumentVersion version)
    {
        if (version.PageCount is not int pageCount || pageCount < 1)
        {
            throw new NotesV3Exception(409, "PDF chưa có số trang hợp lệ.");
        }

        return new PdfDetailsDto(
            document.DocumentId,
            version.DocumentVersionId,
            document.Title,
            version.OriginalFileName,
            pageCount,
            document.IsArchived,
            Convert.ToBase64String(document.RowVersion));
    }

    private static NotesV3Exception Missing() =>
        new(404, "Không tìm thấy PDF.");

    private static NotesV3Exception Conflict() =>
        new(409, "PDF đã thay đổi. Hãy tải lại trước khi lưu.");
}
