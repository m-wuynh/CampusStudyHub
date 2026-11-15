using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.Pages;

public sealed class PageService(
    IRepository repository,
    INotesV3AccessService access) : IPageService
{
    public async Task<long> CreateAsync(
        long sectionId, string title, CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        await access.RequireSectionAsync(sectionId, ct);

        var lastOrder = await repository.Query<NotesV3Page>()
            .Where(x => x.SectionId == sectionId && !x.IsDeleted)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        var page = new NotesV3Page
        {
            SectionId = sectionId,
            Title = title,
            SortOrder = (lastOrder ?? -1) + 1,
            ContentDeltaJson = TextDelta.FromText(string.Empty)
        };

        await repository.AddAsync(page, ct);
        await repository.SaveChangesAsync(ct);
        return page.PageId;
    }

    public async Task SaveTextAsync(
        long pageId, string text, string? versionToken,
        CancellationToken ct = default)
    {
        var page = await GetForWriteAsync(pageId, versionToken, ct);
        page.ContentDeltaJson = TextDelta.FromText(text);
        await SaveAsync(ct);
    }

    public async Task RenameAsync(
        long pageId, string title, string? versionToken,
        CancellationToken ct = default)
    {
        title = NotesV3Validation.Title(title);
        var page = await GetForWriteAsync(pageId, versionToken, ct);
        page.Title = title;
        await SaveAsync(ct);
    }

    public async Task DeleteAsync(
        long pageId, string? versionToken,
        CancellationToken ct = default)
    {
        var page = await GetForWriteAsync(pageId, versionToken, ct);
        page.IsDeleted = true;
        await SaveAsync(ct);
    }

    private async Task<NotesV3Page> GetForWriteAsync(
        long pageId, string? versionToken, CancellationToken ct)
    {
        var allowed = await access.RequirePageAsync(
            pageId, forWrite: true, cancellationToken: ct);

        if (allowed.Page.SectionId is null)
        {
            throw new NotesV3Exception(400, "Trang này thuộc PDF, không thuộc notebook.");
        }

        if (string.IsNullOrWhiteSpace(versionToken))
        {
            throw Conflict();
        }

        var expected = NotesV3Validation.Version(versionToken);
        var page = await repository.Query<NotesV3Page>(false)
            .SingleAsync(x => x.PageId == pageId && !x.IsDeleted, ct);

        if (!page.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }

        return page;
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

    private static NotesV3Exception Conflict() =>
        new(409, "Trang đã thay đổi. Hãy tải lại trước khi lưu.");
}
