using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.Dashboard;

public sealed class NotesDashboardService(
    IRepository repository,
    ICurrentUser currentUser) : INotesDashboardService
{
    public async Task<NotesDashboardDto> GetAsync(
        CancellationToken ct = default)
    {
        if (currentUser.UserId <= 0)
        {
            throw new NotesV3Exception(
                401,
                "Bạn cần đăng nhập.");
        }

        var documents = repository.Query<NotesV3Document>();
        var notebooks = repository.Query<NotesV3Notebook>();
        var shares = repository.Query<NotesV3DocumentShare>();

        var groups = repository.Query<StudyGroup>();
        var members = repository.Query<GroupMember>();

        var eligibleGroupIds =
            from studyGroup in groups
            join membership in members
                on studyGroup.StudyGroupId
                equals membership.StudyGroupId
            where !studyGroup.IsArchived
                  && membership.UserId == currentUser.UserId
                  && membership.Status == "Active"
            select studyGroup.StudyGroupId;

        var personalPdfCount = await documents
            .CountAsync(
                document =>
                    document.OwnerUserId == currentUser.UserId &&
                    !document.IsDeleted &&
                    !document.IsArchived,
                ct);

        var personalNotebookCount = await notebooks
            .CountAsync(
                notebook =>
                    notebook.OwnerUserId == currentUser.UserId &&
                    notebook.StudyGroupId == null &&
                    !notebook.IsDeleted,
                ct);

        var sharedPdfCount = await shares
            .CountAsync(
                share =>
                    share.IsActive &&
                    eligibleGroupIds.Contains(share.StudyGroupId) &&
                    documents.Any(document =>
                        document.DocumentId == share.DocumentId &&
                        !document.IsDeleted &&
                        !document.IsArchived),
                ct);

        var groupNotebookCount = await notebooks
            .CountAsync(
                notebook =>
                    !notebook.IsDeleted &&
                    notebook.StudyGroupId.HasValue &&
                    eligibleGroupIds.Contains(
                        notebook.StudyGroupId.Value),
                ct);

        return new NotesDashboardDto(
            personalPdfCount,
            personalNotebookCount,
            sharedPdfCount,
            groupNotebookCount);
    }
}