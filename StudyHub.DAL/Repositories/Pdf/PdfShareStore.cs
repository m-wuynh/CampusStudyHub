using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Persistence;

namespace StudyHub.DAL.Repositories.Pdf;

/// <summary>
/// Thin DB writer cho PdfDocumentShare.
/// Không chứa business logic (ownership check, auth) —
/// những rule đó thuộc về BLL.PdfSharingService.
/// </summary>
public sealed class PdfShareStore(StudyHubDbContext db)
{
    /// <summary>
    /// Tạo một bản ghi share mới (IsActive = true).
    /// Caller (BLL) phải đảm bảo ownership và uniqueness trước khi gọi.
    /// </summary>
    public async Task CreateShareAsync(
        long documentId,
        long groupId,
        long sharedByUserId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        db.PdfDocumentShares.Add(new PdfDocumentShare
        {
            DocumentId = documentId,
            StudyGroupId = groupId,
            SharedByUserId = sharedByUserId,
            IsActive = true,
            SharedAtUtc = nowUtc
        });

        var document = await db.PdfDocuments
            .SingleOrDefaultAsync(
                x => x.DocumentId == documentId && !x.IsDeleted,
                cancellationToken)
            ?? throw new PdfShareStoreException("not_found",
                "Không tìm thấy PDF hoặc PDF đã bị xóa.");

        document.UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Đánh IsActive = false cho share đang hoạt động.
    /// Caller (BLL) phải đảm bảo permission trước khi gọi.
    /// </summary>
    public async Task DeactivateShareAsync(
        long documentId,
        long groupId,
        CancellationToken cancellationToken = default)
    {
        var document = await db.PdfDocuments
            .SingleOrDefaultAsync(
                x => x.DocumentId == documentId && !x.IsDeleted,
                cancellationToken)
            ?? throw new PdfShareStoreException("not_found",
                "Không tìm thấy PDF hoặc PDF đã bị xóa.");

        var share = await db.PdfDocumentShares
            .SingleOrDefaultAsync(
                x => x.DocumentId == documentId
                     && x.StudyGroupId == groupId
                     && x.IsActive,
                cancellationToken)
            ?? throw new PdfShareStoreException("not_found",
                "Không tìm thấy lượt chia sẻ đang hoạt động.");

        var nowUtc = DateTime.UtcNow;
        share.IsActive = false;
        share.UnsharedAtUtc = nowUtc;
        document.UpdatedAtUtc = nowUtc;
    }
}

public sealed class PdfShareStoreException(
    string code,
    string message) : Exception(message)
{
    public string Code { get; } = code;
}
