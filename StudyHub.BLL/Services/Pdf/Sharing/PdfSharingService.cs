using StudyHub.DAL.Repositories.Pdf;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;
using StudyHub.BLL.Services.Pdf.Sharing;

namespace StudyHub.BLL.Services.Pdf.Sharing;

/// <summary>
/// Xử lý toàn bộ business logic của việc chia sẻ / gỡ chia sẻ PDF vào nhóm học.
/// </summary>
public sealed class PdfSharingService(
    IRepository repository,
    ICurrentUser currentUser,
    PdfShareStore store) : IPdfSharingService
{
    /// <inheritdoc/>
    public async Task ShareAsync(
        long documentId,
        long groupId,
        long actorUserId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated(actorUserId);

        var document = await RequireOwnedDocumentAsync(
            documentId, actorUserId, cancellationToken);

        await RequireNoActiveShareInGroupAsync(
            documentId, groupId, cancellationToken);

        try
        {
            await using var tx = await repository.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            // Recheck inside transaction để tránh race condition.
            await RequireNoActiveShareInGroupAsync(
                documentId, groupId, cancellationToken);

            await store.CreateShareAsync(
                documentId, groupId, actorUserId, cancellationToken);

            await repository.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            throw new PdfException(
                409,
                "Có thao tác chia sẻ khác đang được xử lý. Hãy thử lại.");
        }
    }

    /// <inheritdoc/>
    public async Task UnshareAsync(
        long documentId,
        long groupId,
        long actorUserId,
        bool canManageGroup,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated(actorUserId);

        var document = await RequireExistingDocumentAsync(
            documentId, cancellationToken);

        // Business rule: chỉ Owner hoặc Group Manager được gỡ share.
        if (document.OwnerUserId != actorUserId && !canManageGroup)
        {
            throw new PdfException(
                403,
                "Bạn không có quyền gỡ chia sẻ PDF này.");
        }

        try
        {
            await store.DeactivateShareAsync(
                documentId, groupId, cancellationToken);

            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (PdfShareStoreException ex)
        {
            throw ex.Code switch
            {
                "not_found" => new PdfException(404, ex.Message),
                _ => new PdfException(400, ex.Message)
            };
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<PdfDocument> RequireOwnedDocumentAsync(
        long documentId,
        long actorUserId,
        CancellationToken ct)
    {
        var document = await repository.Query<PdfDocument>()
            .SingleOrDefaultAsync(
                x => x.DocumentId == documentId && !x.IsDeleted, ct)
            ?? throw new PdfException(404, "Không tìm thấy PDF.");

        if (document.OwnerUserId != actorUserId)
        {
            throw new PdfException(403, "Bạn chỉ có thể chia sẻ PDF của mình.");
        }

        return document;
    }

    private async Task<PdfDocument> RequireExistingDocumentAsync(
        long documentId,
        CancellationToken ct) =>
        await repository.Query<PdfDocument>()
            .SingleOrDefaultAsync(
                x => x.DocumentId == documentId && !x.IsDeleted, ct)
        ?? throw new PdfException(404, "Không tìm thấy PDF.");

    private async Task RequireNoActiveShareInGroupAsync(
        long documentId,
        long groupId,
        CancellationToken ct)
    {
        var alreadyShared = await repository.Query<PdfDocumentShare>()
            .AnyAsync(
                x => x.DocumentId == documentId
                     && x.StudyGroupId == groupId
                     && x.IsActive,
                ct);

        if (alreadyShared)
        {
            throw new PdfException(
                409,
                "PDF đã được chia sẻ vào nhóm này.");
        }
    }

    private void RequireAuthenticated(long actorUserId)
    {
        if (actorUserId <= 0 || currentUser.UserId <= 0)
        {
            throw new PdfException(401, "Bạn cần đăng nhập.");
        }
    }

    private static bool IsDeadlock(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
                return true;
        }

        return false;
    }
}
