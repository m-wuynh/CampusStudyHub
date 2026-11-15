using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Pdf.Access;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Pdf.Access;

internal sealed record PdfAccessContext(
    PdfDocument Document,
    long? GroupId);

public sealed class PdfAccessService(
    IRepository repository,
    ICurrentUser currentUser)
{
    private async Task RequireGroupAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        if (groupId <= 0)
        {
            throw Missing();
        }

        var groupExists = await repository
            .Query<StudyGroup>()
            .AnyAsync(
                group => group.StudyGroupId == groupId &&
                         !group.IsArchived,
                cancellationToken);

        if (!groupExists)
        {
            throw Missing();
        }

        var isActiveMember = await repository
            .Query<GroupMember>()
            .AnyAsync(
                member => member.StudyGroupId == groupId &&
                          member.UserId == currentUser.UserId &&
                          member.Status == "Active",
                cancellationToken);

        if (!isActiveMember)
        {
            throw new PdfException(
                403,
                "Bạn không phải thành viên đang hoạt động của nhóm.");
        }
    }

    private async Task<PdfDocument> RequireOwnedPdfAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        var document = await repository
            .Query<PdfDocument>()
            .SingleOrDefaultAsync(
                item => item.DocumentId == documentId &&
                        item.OwnerUserId == currentUser.UserId &&
                        !item.IsDeleted,
                cancellationToken)
            ?? throw Missing();

        return document;
    }

    internal async Task<PdfAccessContext> RequirePdfAsync(
        long documentId,
        long? groupDocumentShareId = null,
        bool forWrite = false,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        if (groupDocumentShareId is not long shareId)
        {
            var ownedDocument = await RequireOwnedPdfAsync(
                documentId,
                cancellationToken);
            return new PdfAccessContext(ownedDocument, null);
        }

        var sharedPdf = await (
            from share in repository.Query<PdfDocumentShare>()
            join document in repository.Query<PdfDocument>()
                on share.DocumentId equals document.DocumentId
            where share.PdfDocumentShareId == shareId &&
                  share.DocumentId == documentId &&
                  share.IsActive &&
                  !document.IsDeleted
            select new { Document = document, GroupId = share.StudyGroupId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Missing();

        await RequireGroupAsync(
            sharedPdf.GroupId,
            cancellationToken);

        if (forWrite)
        {
            throw new PdfException(
                403,
                "PDF chia sẻ trong nhóm học chỉ có thể xem, không thể chỉnh sửa.");
        }

        return new PdfAccessContext(sharedPdf.Document, sharedPdf.GroupId);
    }

    internal async Task<PdfAccessContext> RequirePdfPageAsync(
        long documentId,
        int pageNumber,
        long? groupDocumentShareId = null,
        bool forWrite = false,
        CancellationToken cancellationToken = default)
    {
        var context = await RequirePdfAsync(
            documentId,
            groupDocumentShareId,
            forWrite,
            cancellationToken);
        if (pageNumber < 1 || pageNumber > context.Document.PageCount)
        {
            throw new PdfException(400, "Trang PDF không hợp lệ.");
        }

        return context;
    }

    private void RequireAuthenticated()
    {
        if (currentUser.UserId <= 0)
        {
            throw new PdfException(
                401,
                "Bạn cần đăng nhập.");
        }
    }

    private static PdfException Missing() =>
        new(
            404,
            "Không tìm thấy tài nguyên hoặc tài nguyên không còn khả dụng.");
}
