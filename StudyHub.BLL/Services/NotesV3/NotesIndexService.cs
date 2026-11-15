using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3;

public sealed class NotesIndexService(
    IRepository repository,
    ICurrentUser currentUser) : INotesIndexService
{
    public async Task<NotesTrashDto> GetTrashAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId <= 0)
        {
            throw new NotesV3Exception(401, "Bạn cần đăng nhập.");
        }

        var nowUtc = DateTime.UtcNow;
        var notebookRows = await repository.Query<NotesV3Notebook>()
            .Where(x => x.OwnerUserId == userId &&
                        x.StudyGroupId == null &&
                        x.IsDeleted)
            .OrderByDescending(x => x.NotebookId)
            .Select(x => new { x.NotebookId, x.Title, x.DeletedAtUtc })
            .ToListAsync(ct);

        var notebooks = notebookRows
            .Select(x => new TrashedNotebookDto(
                x.NotebookId, x.Title,
                NotesTrashPolicy.DaysLeft(x.DeletedAtUtc, nowUtc)))
            .ToList();

        var versions = repository.Query<NotesV3DocumentVersion>();
        var pdfRows = await (
            from document in repository.Query<NotesV3Document>()
            from version in versions
                .Where(x => x.DocumentId == document.DocumentId)
                .OrderByDescending(x => x.DocumentVersionId)
                .Take(1)
            where document.OwnerUserId == userId && document.IsDeleted
            orderby document.UpdatedAtUtc descending
            select new
            {
                document.DocumentId,
                document.Title,
                document.DeletedAtUtc,
                document.RowVersion,
                version.OriginalFileName
            }).ToListAsync(ct);

        var pdfs = pdfRows.Select(x => new TrashedPdfDto(
            x.DocumentId,
            x.Title,
            x.OriginalFileName,
            Convert.ToBase64String(x.RowVersion),
            NotesTrashPolicy.DaysLeft(x.DeletedAtUtc, nowUtc))).ToList();

        return new NotesTrashDto(notebooks, pdfs);
    }

    public async Task<NotesIndexDto> GetAsync(
        long? groupId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;

        if (userId <= 0)
        {
            throw new NotesV3Exception(401, "Bạn cần đăng nhập.");
        }

        var notebooks = await repository.Query<NotesV3Notebook>()
            .Where(x => x.OwnerUserId == userId &&
                        x.StudyGroupId == null &&
                        !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.NotebookId)
            .Select(x => new NotebookSummaryDto(x.NotebookId, x.Title))
            .ToListAsync(ct);

        var documents = repository.Query<NotesV3Document>()
            .Where(x => x.OwnerUserId == userId && !x.IsDeleted);

        var versions = repository.Query<NotesV3DocumentVersion>();

        var pdfRows = await (
            from document in documents
            from version in versions
                .Where(x => x.DocumentId == document.DocumentId)
                .OrderByDescending(x => x.DocumentVersionId)
                .Take(1)
            orderby document.UpdatedAtUtc descending
            select new
            {
                document.DocumentId,
                document.Title,
                document.IsArchived,
                document.RowVersion,
                version.OriginalFileName,
                version.PageCount
            }).ToListAsync(ct);

        var pdfs = pdfRows
            .Select(x => new PdfSummaryDto(
                x.DocumentId,
                x.Title,
                x.OriginalFileName,
                x.PageCount ?? 0,
                x.IsArchived,
                Convert.ToBase64String(x.RowVersion)))
            .ToList();

        var groups = await (
            from studyGroup in repository.Query<StudyGroup>()
            join member in repository.Query<GroupMember>()
                on studyGroup.StudyGroupId equals member.StudyGroupId
            where member.UserId == userId &&
                  member.Status == "Active" &&
                  !studyGroup.IsArchived
            orderby studyGroup.GroupName
            select new GroupSummaryDto(
                studyGroup.StudyGroupId,
                studyGroup.GroupName))
            .ToListAsync(ct);

        var groupIds = groups.Select(x => x.Id).ToList();
        var allDocuments = repository.Query<NotesV3Document>();

        var sharedPdfCount = await repository
            .Query<NotesV3DocumentShare>()
            .CountAsync(x => x.IsActive &&
                             groupIds.Contains(x.StudyGroupId) &&
                             allDocuments.Any(document =>
                                 document.DocumentId == x.DocumentId &&
                                 !document.IsDeleted &&
                                 !document.IsArchived), ct);

        var groupNotebookCount = await repository
            .Query<NotesV3Notebook>()
            .CountAsync(x => !x.IsDeleted &&
                             x.StudyGroupId.HasValue &&
                             groupIds.Contains(x.StudyGroupId.Value), ct);

        GroupSummaryDto? selectedGroup = null;
        IReadOnlyList<NotebookSummaryDto> groupNotebooks = [];
        IReadOnlyList<SharedPdfSummaryDto> groupPdfs = [];

        if (groupId is long selectedGroupId)
        {
            selectedGroup = groups.SingleOrDefault(x => x.Id == selectedGroupId)
                ?? throw new NotesV3Exception(404, "Không tìm thấy nhóm của bạn.");

            groupNotebooks = await repository.Query<NotesV3Notebook>()
                .Where(x => x.StudyGroupId == selectedGroupId && !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .Select(x => new NotebookSummaryDto(x.NotebookId, x.Title))
                .ToListAsync(ct);

            groupPdfs = await (
                from share in repository.Query<NotesV3DocumentShare>()
                join document in allDocuments
                    on share.DocumentId equals document.DocumentId
                from version in versions
                    .Where(x => x.DocumentId == document.DocumentId)
                    .OrderByDescending(x => x.DocumentVersionId)
                    .Take(1)
                where share.StudyGroupId == selectedGroupId &&
                      share.IsActive &&
                      !document.IsDeleted &&
                      !document.IsArchived
                orderby share.SharedAtUtc descending
                select new SharedPdfSummaryDto(
                    document.DocumentId,
                    share.GroupDocumentShareId,
                    document.Title,
                    version.OriginalFileName,
                    version.PageCount ?? 0))
                .ToListAsync(ct);
        }

        return new NotesIndexDto(
            NotebookCount: notebooks.Count,
            PdfCount: pdfs.Count,
            SharedCount: sharedPdfCount + groupNotebookCount,
            Notebooks: notebooks,
            Pdfs: pdfs,
            Groups: groups,
            SelectedGroup: selectedGroup,
            GroupNotebooks: groupNotebooks,
            GroupPdfs: groupPdfs);
    }
}
