using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.Notebooks;

public sealed class NotebookService(
    IRepository repository,
    ICurrentUser currentUser,
    INotesV3AccessService access) : INotebookService
{
    public async Task<NotebookScreenDto> GetScreenAsync(
        long notebookId, long? pageId, long? sectionId,
        CancellationToken ct = default)
    {
        var notebook = await access.RequireNotebookAsync(notebookId, ct);

        var sections = await repository.Query<NotesV3Section>()
            .Where(x => x.NotebookId == notebookId && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.SectionId)
            .ToListAsync(ct);

        var sectionIds = sections.Select(x => x.SectionId).ToList();

        var pages = await repository.Query<NotesV3Page>()
            .Where(x => !x.IsDeleted &&
                        x.SectionId.HasValue &&
                        sectionIds.Contains(x.SectionId.Value))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.PageId)
            .ToListAsync(ct);

        var sectionItems = sections
            .Select(section => new NotebookSectionDto(
                section.SectionId,
                section.Title,
                pages.Where(page => page.SectionId == section.SectionId)
                    .Select(page => new NotebookPageLinkDto(page.PageId, page.Title))
                    .ToList()))
            .ToList();

        var selectedSection = sectionId.HasValue
            ? sections.SingleOrDefault(x => x.SectionId == sectionId.Value)
            : pageId.HasValue
                ? sections.FirstOrDefault(x => pages.Any(page =>
                    page.PageId == pageId.Value && page.SectionId == x.SectionId))
                : sections.FirstOrDefault();

        if (sectionId.HasValue && selectedSection is null)
        {
            throw new NotesV3Exception(404, "Không tìm thấy section trong notebook này.");
        }

        var selectedPage = pageId.HasValue
            ? pages.SingleOrDefault(x => x.PageId == pageId.Value)
            : pages.FirstOrDefault(x => x.SectionId == selectedSection!.SectionId);

        if (pageId.HasValue && selectedPage is null)
        {
            throw new NotesV3Exception(404, "Không tìm thấy trang trong notebook này.");
        }

        var editor = selectedPage is null
            ? null
            : new NotebookPageDto(
                selectedPage.PageId,
                selectedPage.Title,
                TextDelta.ToText(selectedPage.ContentDeltaJson),
                Convert.ToBase64String(selectedPage.RowVersion));

        return new NotebookScreenDto(
            notebook.NotebookId,
            notebook.Title,
            sectionItems,
            selectedSection?.SectionId,
            editor);
    }

    public async Task<long> CreateNotebookAsync(
        string title, long? groupId, CancellationToken ct = default)
    {
        if (currentUser.UserId <= 0)
        {
            throw new NotesV3Exception(401, "Bạn cần đăng nhập.");
        }

        title = NotesV3Validation.Title(title);

        if (groupId is long id)
        {
            await access.RequireGroupAsync(id, ct);
        }

        var query = repository.Query<NotesV3Notebook>()
            .Where(x => !x.IsDeleted);

        query = groupId is long group
            ? query.Where(x => x.StudyGroupId == group)
            : query.Where(x => x.StudyGroupId == null &&
                               x.OwnerUserId == currentUser.UserId);

        var lastOrder = await query
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        var notebook = new NotesV3Notebook
        {
            OwnerUserId = currentUser.UserId,
            StudyGroupId = groupId,
            Title = title,
            SortOrder = (lastOrder ?? -1) + 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(notebook, ct);
        await repository.SaveChangesAsync(ct);
        return notebook.NotebookId;
    }

    public async Task RenameNotebookAsync(
        long notebookId, string title, CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        await access.RequireNotebookAsync(notebookId, ct);

        var notebook = await repository.Query<NotesV3Notebook>(false)
            .SingleAsync(x => x.NotebookId == notebookId, ct);

        notebook.Title = title;
        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteNotebookAsync(
        long notebookId, CancellationToken ct = default)
    {
        await access.RequireNotebookAsync(notebookId, ct);

        var notebook = await repository.Query<NotesV3Notebook>(false)
            .SingleAsync(x => x.NotebookId == notebookId, ct);

        notebook.IsDeleted = true;
        notebook.DeletedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(ct);
    }

    public async Task RestoreNotebookAsync(
        long notebookId, CancellationToken ct = default)
    {
        var notebook = await repository.Query<NotesV3Notebook>(false)
            .SingleOrDefaultAsync(x => x.NotebookId == notebookId &&
                                       x.OwnerUserId == currentUser.UserId &&
                                       x.StudyGroupId == null &&
                                       x.IsDeleted, ct)
            ?? throw new NotesV3Exception(404, "Không tìm thấy notebook trong thùng rác.");

        if (NotesTrashPolicy.DaysLeft(notebook.DeletedAtUtc, DateTime.UtcNow) == 0)
        {
            throw new NotesV3Exception(410, "Đã hết thời hạn khôi phục notebook.");
        }

        notebook.IsDeleted = false;
        notebook.DeletedAtUtc = null;
        await repository.SaveChangesAsync(ct);
    }

    public async Task<long> CreateSectionAsync(
        long notebookId, string title, CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        await access.RequireNotebookAsync(notebookId, ct);

        var lastOrder = await repository.Query<NotesV3Section>()
            .Where(x => x.NotebookId == notebookId && !x.IsDeleted)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        var section = new NotesV3Section
        {
            NotebookId = notebookId,
            Title = title,
            SortOrder = (lastOrder ?? -1) + 1
        };

        await repository.AddAsync(section, ct);
        await repository.SaveChangesAsync(ct);
        return section.SectionId;
    }

    public async Task RenameSectionAsync(
        long sectionId, string title, CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        await access.RequireSectionAsync(sectionId, ct);

        var section = await repository.Query<NotesV3Section>(false)
            .SingleAsync(x => x.SectionId == sectionId, ct);

        section.Title = title;
        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteSectionAsync(
        long sectionId, CancellationToken ct = default)
    {
        await access.RequireSectionAsync(sectionId, ct);

        var section = await repository.Query<NotesV3Section>(false)
            .SingleAsync(x => x.SectionId == sectionId, ct);

        section.IsDeleted = true;
        await repository.SaveChangesAsync(ct);
    }
}
