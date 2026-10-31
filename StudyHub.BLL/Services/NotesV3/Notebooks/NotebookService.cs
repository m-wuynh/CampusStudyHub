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
    public async Task<IReadOnlyList<NotebookItem>> ListAsync(
        long? groupId,
        string? search,
        CancellationToken ct = default)
    {
        if (groupId is long id)
        {
            await access.RequireGroupAsync(id, ct);
        }

        var query = NotebookScope(groupId);
        var term = search?.Trim();

        if (!string.IsNullOrEmpty(term))
        {
            if (term.Length > 200)
            {
                throw new NotesV3Exception(
                    400, "Từ khóa tối đa 200 ký tự.");
            }

            var sections = repository.Query<NotesV3Section>();
            var pages = repository.Query<NotesV3Page>();

            query = query.Where(notebook =>
                notebook.Title.Contains(term) ||
                sections.Any(section =>
                    section.NotebookId == notebook.NotebookId &&
                    !section.IsDeleted &&
                    pages.Any(page =>
                        page.SectionId == section.SectionId &&
                        !page.IsDeleted &&
                        page.Title.Contains(term))));
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.NotebookId)
            .Select(x => new NotebookItem(
                x.NotebookId,
                x.Title,
                x.SortOrder,
                x.StudyGroupId))
            .ToListAsync(ct);
    }

    public async Task<NotebookDetails> GetAsync(
        long notebookId,
        CancellationToken ct = default)
    {
        var notebook = await access.RequireNotebookAsync(
            notebookId, ct);

        var sections = await repository
            .Query<NotesV3Section>()
            .Where(x => x.NotebookId == notebookId && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.SectionId)
            .Select(x => new SectionItem(
                x.SectionId, x.Title, x.SortOrder))
            .ToListAsync(ct);

        var pages = await NotebookPages(notebookId)
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.PageId)
            .ToListAsync(ct);

        return new NotebookDetails(
            new NotebookItem(
                notebook.NotebookId,
                notebook.Title,
                notebook.SortOrder,
                notebook.StudyGroupId),
            sections,
            pages.Select(ToItem).ToList());
    }

    public async Task<long> CreateNotebookAsync(
        string title,
        long? groupId,
        CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);

        if (groupId is long id)
        {
            await access.RequireGroupAsync(id, ct);
        }

        var lastOrder = await NotebookScope(groupId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        var notebook = new NotesV3Notebook
        {
            OwnerUserId = currentUser.UserId,
            StudyGroupId = groupId,
            Title = title,
            SortOrder = NextOrder(lastOrder),
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(notebook, ct);
        await CommitAsync(ct);

        return notebook.NotebookId;
    }

    public async Task RenameNotebookAsync(
        long notebookId,
        string title,
        CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        await access.RequireNotebookAsync(notebookId, ct);

        var notebook = await repository
            .Query<NotesV3Notebook>(asNoTracking: false)
            .SingleAsync(x => x.NotebookId == notebookId, ct);

        notebook.Title = title;
        await CommitAsync(ct);
    }

    public async Task DeleteNotebookAsync(
        long notebookId,
        CancellationToken ct = default)
    {
        await access.RequireNotebookAsync(notebookId, ct);

        var notebook = await repository
            .Query<NotesV3Notebook>(asNoTracking: false)
            .SingleAsync(x => x.NotebookId == notebookId, ct);

        // Không xóa dây chuyền. AccessService kiểm tra trạng thái cha.
        notebook.IsDeleted = true;

        await CommitAsync(ct);
    }

    public async Task<long> CreateSectionAsync(
        long notebookId,
        string title,
        CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        await access.RequireNotebookAsync(notebookId, ct);

        var lastOrder = await repository
            .Query<NotesV3Section>()
            .Where(x => x.NotebookId == notebookId && !x.IsDeleted)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        var section = new NotesV3Section
        {
            NotebookId = notebookId,
            Title = title,
            SortOrder = NextOrder(lastOrder)
        };

        await repository.AddAsync(section, ct);
        await CommitAsync(ct);

        return section.SectionId;
    }

    public async Task RenameSectionAsync(
        long sectionId,
        string title,
        CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        await access.RequireSectionAsync(sectionId, ct);

        var section = await repository
            .Query<NotesV3Section>(asNoTracking: false)
            .SingleAsync(x => x.SectionId == sectionId, ct);

        section.Title = title;
        await CommitAsync(ct);
    }

    public async Task DeleteSectionAsync(
        long sectionId,
        CancellationToken ct = default)
    {
        await access.RequireSectionAsync(sectionId, ct);

        var section = await repository
            .Query<NotesV3Section>(asNoTracking: false)
            .SingleAsync(x => x.SectionId == sectionId, ct);

        section.IsDeleted = true;

        await CommitAsync(ct);
    }

    public async Task<long> CreatePageAsync(
        long sectionId,
        string title,
        CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        await access.RequireSectionAsync(sectionId, ct);

        var lastOrder = await repository
            .Query<NotesV3Page>()
            .Where(x => x.SectionId == sectionId && !x.IsDeleted)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        var page = new NotesV3Page
        {
            SectionId = sectionId,
            Title = title,
            SortOrder = NextOrder(lastOrder),
            ContentDeltaJson = NotesV3Validation.EmptyDelta
        };

        await repository.AddAsync(page, ct);
        await CommitAsync(ct);

        // Trang nhóm mới bắt đầu bằng Y.Doc rỗng.
        // Văn bản đầu tiên sẽ được lưu qua CollaborationService.
        return page.PageId;
    }

    public async Task<PageDetails> GetPageAsync(
        long pageId,
        CancellationToken ct = default)
    {
        var result = await access.RequirePageAsync(
            pageId,
            cancellationToken: ct);

        // Service này chỉ xử lý Page thuộc notebook.
        RequireNotebookPage(result.Page);

        return new PageDetails(
            result.Page.PageId,
            result.Page.Title,
            result.IsGroup ? null : result.Page.ContentDeltaJson,
            Convert.ToBase64String(result.Page.RowVersion),
            result.IsGroup);
    }

    public async Task<PageSaveResult> SavePersonalPageAsync(
        long pageId,
        PageSaveRequest request,
        CancellationToken ct = default)
    {
        var result = await access.RequirePageAsync(
            pageId,
            forWrite: true,
            cancellationToken: ct);

        RequireNotebookPage(result.Page);

        if (result.IsGroup)
        {
            throw new NotesV3Exception(
                409, "Nội dung nhóm phải được lưu qua cộng tác Yjs.");
        }

        var delta = NotesV3Validation.Delta(request.DeltaJson);

        var page = await TrackedPageAsync(
            pageId, request.VersionToken, ct);

        page.ContentDeltaJson = delta.Json;

        await CommitAsync(ct);

        return new PageSaveResult(
            Convert.ToBase64String(page.RowVersion));
    }

    public async Task RenamePageAsync(
        long pageId,
        string title,
        string versionToken,
        CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);

        var result = await access.RequirePageAsync(
            pageId,
            forWrite: true,
            cancellationToken: ct);

        RequireNotebookPage(result.Page);

        var page = await TrackedPageAsync(pageId, versionToken, ct);
        page.Title = title;

        await CommitAsync(ct);
    }

    public async Task DeletePageAsync(
        long pageId,
        string versionToken,
        CancellationToken ct = default)
    {
        var result = await access.RequirePageAsync(
            pageId,
            forWrite: true,
            cancellationToken: ct);

        RequireNotebookPage(result.Page);

        var page = await TrackedPageAsync(pageId, versionToken, ct);
        page.IsDeleted = true;

        await CommitAsync(ct);
    }

    public async Task<IReadOnlyList<PageItem>> ListTrashAsync(
        long notebookId,
        CancellationToken ct = default)
    {
        var notebook = await access.RequireNotebookAsync(
            notebookId, ct);

        RequirePersonalNotebook(notebook);

        var pages = await NotebookPages(notebookId)
            .Where(x => x.IsDeleted)
            .OrderBy(x => x.Title)
            .ThenBy(x => x.PageId)
            .ToListAsync(ct);

        return pages.Select(ToItem).ToList();
    }

    public async Task RestorePageAsync(
        long pageId,
        string versionToken,
        CancellationToken ct = default)
    {
        var result = await access.RequirePageAsync(
            pageId,
            includeDeleted: true,
            forWrite: true,
            cancellationToken: ct);

        RequireNotebookPage(result.Page);

        if (result.IsGroup)
        {
            throw new NotesV3Exception(
                400, "Thùng rác hiện chỉ hỗ trợ trang cá nhân.");
        }

        var page = await TrackedPageAsync(pageId, versionToken, ct);
        page.IsDeleted = false;

        await CommitAsync(ct);
    }

    public async Task MovePageAsync(
        long pageId,
        long targetSectionId,
        string versionToken,
        CancellationToken ct = default)
    {
        var source = await access.RequirePageAsync(
            pageId,
            forWrite: true,
            cancellationToken: ct);

        RequireNotebookPage(source.Page);

        var targetSection = await access.RequireSectionAsync(
            targetSectionId, ct);

        var targetNotebook = await access.RequireNotebookAsync(
            targetSection.NotebookId, ct);

        // Không biến trang cá nhân thành trang nhóm bằng thao tác move.
        // Chia sẻ phải đi qua quy trình copy + CRDT seed riêng.
        if (source.StudyGroupId != targetNotebook.StudyGroupId)
        {
            throw new NotesV3Exception(
                400,
                "Chỉ chuyển trang trong cùng phạm vi cá nhân hoặc cùng nhóm.");
        }

        var lastOrder = await repository
            .Query<NotesV3Page>()
            .Where(x => x.SectionId == targetSectionId && !x.IsDeleted)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        var page = await TrackedPageAsync(pageId, versionToken, ct);

        page.SectionId = targetSectionId;
        page.SortOrder = NextOrder(lastOrder);

        await CommitAsync(ct);
    }

    public async Task ReorderNotebooksAsync(
        long? groupId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default)
    {
        if (groupId is long id)
        {
            await access.RequireGroupAsync(id, ct);
        }

        var notebooks = await NotebookScope(
                groupId, asNoTracking: false)
            .ToListAsync(ct);

        var positions = Positions(
            orderedIds,
            notebooks.Select(x => x.NotebookId));

        foreach (var notebook in notebooks)
        {
            notebook.SortOrder = positions[notebook.NotebookId];
        }

        await CommitAsync(ct);
    }

    public async Task ReorderSectionsAsync(
        long notebookId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default)
    {
        await access.RequireNotebookAsync(notebookId, ct);

        var sections = await repository
            .Query<NotesV3Section>(asNoTracking: false)
            .Where(x => x.NotebookId == notebookId && !x.IsDeleted)
            .ToListAsync(ct);

        var positions = Positions(
            orderedIds,
            sections.Select(x => x.SectionId));

        foreach (var section in sections)
        {
            section.SortOrder = positions[section.SectionId];
        }

        await CommitAsync(ct);
    }

    public async Task ReorderPagesAsync(
        long sectionId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default)
    {
        await access.RequireSectionAsync(sectionId, ct);

        var pages = await repository
            .Query<NotesV3Page>(asNoTracking: false)
            .Where(x => x.SectionId == sectionId && !x.IsDeleted)
            .ToListAsync(ct);

        var positions = Positions(
            orderedIds,
            pages.Select(x => x.PageId));

        foreach (var page in pages)
        {
            page.SortOrder = positions[page.PageId];
        }

        await CommitAsync(ct);
    }

    private IQueryable<NotesV3Notebook> NotebookScope(
        long? groupId,
        bool asNoTracking = true)
    {
        var query = repository
            .Query<NotesV3Notebook>(asNoTracking)
            .Where(x => !x.IsDeleted);

        return groupId is long id
            ? query.Where(x => x.StudyGroupId == id)
            : query.Where(x =>
                x.StudyGroupId == null &&
                x.OwnerUserId == currentUser.UserId);
    }

    private IQueryable<NotesV3Page> NotebookPages(long notebookId)
    {
        var sections = repository
            .Query<NotesV3Section>()
            .Where(x => x.NotebookId == notebookId && !x.IsDeleted);

        return repository
            .Query<NotesV3Page>()
            .Where(page => sections.Any(
                section => page.SectionId == section.SectionId));
    }

    private async Task<NotesV3Page> TrackedPageAsync(
        long pageId,
        string versionToken,
        CancellationToken ct)
    {
        var expected = NotesV3Validation.Version(versionToken);

        var page = await repository
            .Query<NotesV3Page>(asNoTracking: false)
            .SingleOrDefaultAsync(x => x.PageId == pageId, ct)
            ?? throw new NotesV3Exception(
                404, "Không tìm thấy trang.");

        if (!page.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }

        return page;
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

    private static Dictionary<long, int> Positions(
        IReadOnlyList<long> orderedIds,
        IEnumerable<long> actualIds)
    {
        var actual = actualIds.ToHashSet();

        if (orderedIds.Count != actual.Count ||
            orderedIds.Distinct().Count() != orderedIds.Count ||
            !actual.SetEquals(orderedIds))
        {
            throw new NotesV3Exception(
                400,
                "Danh sách sắp xếp phải chứa đủ các mục hiện tại, không trùng ID.");
        }

        return orderedIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);
    }

    private static int NextOrder(int? lastOrder)
    {
        if (lastOrder == int.MaxValue)
        {
            throw new NotesV3Exception(
                409, "Hãy sắp xếp lại danh sách trước khi thêm mục.");
        }

        return lastOrder is int value ? value + 1 : 0;
    }

    private static void RequireNotebookPage(NotesV3Page page)
    {
        if (page.SectionId is null)
        {
            throw new NotesV3Exception(
                400, "Ghi chú trang PDF phải dùng chức năng Page Notes.");
        }
    }

    private static void RequirePersonalNotebook(
        NotesV3Notebook notebook)
    {
        if (notebook.StudyGroupId.HasValue)
        {
            throw new NotesV3Exception(
                400, "Thùng rác hiện chỉ hỗ trợ notebook cá nhân.");
        }
    }

    private static PageItem ToItem(NotesV3Page page) =>
        new(
            page.PageId,
            page.SectionId!.Value,
            page.Title,
            page.SortOrder,
            Convert.ToBase64String(page.RowVersion));

    private static NotesV3Exception Conflict() =>
        new(
            409,
            "Trang đã thay đổi ở nơi khác. Giữ bản nháp và tải lại trước khi lưu.");
}