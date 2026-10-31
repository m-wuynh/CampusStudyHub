using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3;

public sealed class NotesV3AccessService(
    IRepository repository,
    ICurrentUser currentUser) : INotesV3AccessService
{
    public async Task RequireGroupAsync(
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
            throw new NotesV3Exception(
                403,
                "Bạn không phải thành viên đang hoạt động của nhóm.");
        }
    }

    public async Task<NotesV3Notebook> RequireNotebookAsync(
        long notebookId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        var notebook = await repository
            .Query<NotesV3Notebook>()
            .SingleOrDefaultAsync(
                item => item.NotebookId == notebookId &&
                        !item.IsDeleted,
                cancellationToken)
            ?? throw Missing();

        if (notebook.StudyGroupId is long groupId)
        {
            // Người tạo không có đặc quyền riêng trong notebook nhóm.
            await RequireGroupAsync(groupId, cancellationToken);
        }
        else if (notebook.OwnerUserId != currentUser.UserId)
        {
            throw Missing();
        }

        return notebook;
    }

    public async Task<NotesV3Section> RequireSectionAsync(
        long sectionId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        var section = await repository
            .Query<NotesV3Section>()
            .SingleOrDefaultAsync(
                item => item.SectionId == sectionId &&
                        !item.IsDeleted,
                cancellationToken)
            ?? throw Missing();

        await RequireNotebookAsync(
            section.NotebookId,
            cancellationToken);

        return section;
    }

    public async Task<NotesV3PageAccess> RequirePageAsync(
        long pageId,
        bool includeDeleted = false,
        bool forWrite = false,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        var page = await repository
            .Query<NotesV3Page>()
            .SingleOrDefaultAsync(
                item => item.PageId == pageId &&
                        (includeDeleted || !item.IsDeleted),
                cancellationToken)
            ?? throw Missing();

        if (page.SectionId is long sectionId)
        {
            var section = await RequireSectionAsync(
                sectionId,
                cancellationToken);

            // Section đã được kiểm tra cùng notebook cha.
            var groupId = await repository
                .Query<NotesV3Notebook>()
                .Where(item => item.NotebookId == section.NotebookId)
                .Select(item => item.StudyGroupId)
                .SingleAsync(cancellationToken);

            return new NotesV3PageAccess(
                page,
                groupId.HasValue,
                groupId);
        }

        var link = await repository
            .Query<NotesV3PdfPageLink>()
            .SingleOrDefaultAsync(
                item => item.PageId == pageId,
                cancellationToken)
            ?? throw Missing();

        if (link.GroupDocumentShareId is null &&
            link.OwnerUserId != currentUser.UserId)
        {
            throw Missing();
        }

        await RequirePdfAsync(
            link.DocumentVersionId,
            link.GroupDocumentShareId,
            forWrite,
            cancellationToken);

        if (link.GroupDocumentShareId is not long shareId)
        {
            return new NotesV3PageAccess(
                page,
                false,
                null);
        }

        var shareGroupId = await repository
            .Query<NotesV3DocumentShare>()
            .Where(item =>
                item.GroupDocumentShareId == shareId &&
                item.IsActive)
            .Select(item => (long?)item.StudyGroupId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Missing();

        return new NotesV3PageAccess(
            page,
            true,
            shareGroupId,
            shareId);
    }

    public async Task<NotesV3Document> RequireOwnedPdfAsync(
        long documentId,
        bool forWrite = false,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        var document = await repository
            .Query<NotesV3Document>()
            .SingleOrDefaultAsync(
                item => item.DocumentId == documentId &&
                        item.OwnerUserId == currentUser.UserId &&
                        !item.IsDeleted,
                cancellationToken)
            ?? throw Missing();

        EnsureWritable(document, forWrite);

        return document;
    }

    public async Task<NotesV3Document> RequirePdfAsync(
        long documentVersionId,
        long? groupDocumentShareId = null,
        bool forWrite = false,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        var documentId = await repository
            .Query<NotesV3DocumentVersion>()
            .Where(item =>
                item.DocumentVersionId == documentVersionId)
            .Select(item => (long?)item.DocumentId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Missing();

        if (groupDocumentShareId is not long shareId)
        {
            return await RequireOwnedPdfAsync(
                documentId,
                forWrite,
                cancellationToken);
        }

        var share = await repository
            .Query<NotesV3DocumentShare>()
            .SingleOrDefaultAsync(
                item => item.GroupDocumentShareId == shareId &&
                        item.DocumentId == documentId &&
                        item.IsActive,
                cancellationToken)
            ?? throw Missing();

        await RequireGroupAsync(
            share.StudyGroupId,
            cancellationToken);

        // Group scope không cho truy cập PDF đã xóa/lưu trữ.
        var document = await repository
            .Query<NotesV3Document>()
            .SingleOrDefaultAsync(
                item => item.DocumentId == documentId &&
                        !item.IsDeleted &&
                        !item.IsArchived,
                cancellationToken)
            ?? throw Missing();

        return document;
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

    private static void EnsureWritable(
        NotesV3Document document,
        bool forWrite)
    {
        if (forWrite && document.IsArchived)
        {
            throw new NotesV3Exception(
                409,
                "PDF đã lưu trữ, chỉ có thể xem.");
        }
    }

    private static NotesV3Exception Missing() =>
        new(
            404,
            "Không tìm thấy tài nguyên hoặc tài nguyên không còn khả dụng.");
}