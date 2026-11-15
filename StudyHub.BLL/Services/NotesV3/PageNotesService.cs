using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.PagesNotes;

public sealed class PageNotesService(
    IRepository repository,
    ICurrentUser currentUser,
    INotesV3AccessService access) : IPageNotesService
{
    public async Task<PdfPageNoteDto> GetTextAsync(
        long versionId, int pageNumber, long? shareId,
        CancellationToken ct = default)
    {
        await access.RequirePdfAsync(versionId, shareId, cancellationToken: ct);
        await RequirePageNumberAsync(versionId, pageNumber, ct);

        var link = await FindLinkAsync(versionId, pageNumber, shareId, ct);
        if (link is null)
        {
            return new PdfPageNoteDto(string.Empty, null);
        }

        var page = await GetPageAsync(link.PageId, ct);
        return new PdfPageNoteDto(
            TextDelta.ToText(page.ContentDeltaJson),
            Convert.ToBase64String(page.RowVersion));
    }

    public async Task<IReadOnlyList<PdfBookPageDto>> ListBookTextAsync(
        long versionId, long? shareId,
        CancellationToken ct = default)
    {
        await access.RequirePdfAsync(versionId, shareId, cancellationToken: ct);

        long? ownerId = shareId.HasValue ? null : currentUser.UserId;
        var links = repository.Query<NotesV3PdfPageLink>();
        var pages = repository.Query<NotesV3Page>();

        var rows = await (
            from link in links
            join page in pages on link.PageId equals page.PageId
            where link.DocumentVersionId == versionId &&
                  link.OwnerUserId == ownerId &&
                  link.GroupDocumentShareId == shareId &&
                  !page.IsDeleted &&
                  page.SectionId == null
            orderby link.PhysicalPageNumber
            select new
            {
                link.PhysicalPageNumber,
                page.ContentDeltaJson
            }).ToListAsync(ct);

        return rows
            .Select(row => new PdfBookPageDto(
                row.PhysicalPageNumber,
                TextDelta.ToText(row.ContentDeltaJson)))
            .Where(row => !string.IsNullOrWhiteSpace(row.Text))
            .ToList();
    }

    public async Task SaveTextAsync(
        long versionId, int pageNumber, string text,
        string? versionToken, long? shareId,
        CancellationToken ct = default)
    {
        await access.RequirePdfAsync(
            versionId, shareId, forWrite: true, cancellationToken: ct);
        await RequirePageNumberAsync(versionId, pageNumber, ct);

        var deltaJson = TextDelta.FromText(text);
        var link = await FindLinkAsync(versionId, pageNumber, shareId, ct);

        if (link is not null)
        {
            await UpdateAsync(link.PageId, versionToken, deltaJson, ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(versionToken))
        {
            throw Conflict();
        }

        // Mở một trang trống không tạo bản ghi mới.
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await using var transaction = await repository.BeginTransactionAsync(ct);

        try
        {
            var page = new NotesV3Page
            {
                SectionId = null,
                Title = $"Trang {pageNumber}",
                ContentDeltaJson = deltaJson
            };

            await repository.AddAsync(page, ct);
            await repository.SaveChangesAsync(ct);

            var newLink = new NotesV3PdfPageLink
            {
                PageId = page.PageId,
                DocumentVersionId = versionId,
                OwnerUserId = shareId.HasValue ? null : currentUser.UserId,
                GroupDocumentShareId = shareId,
                PhysicalPageNumber = pageNumber
            };

            await repository.AddAsync(newLink, ct);
            await repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            throw Conflict();
        }
    }

    private async Task UpdateAsync(
        long pageId, string? versionToken, string deltaJson,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(versionToken))
        {
            throw Conflict();
        }

        var expected = NotesV3Validation.Version(versionToken);
        var page = await repository.Query<NotesV3Page>(false)
            .SingleOrDefaultAsync(x => x.PageId == pageId &&
                                       x.SectionId == null &&
                                       !x.IsDeleted, ct)
            ?? throw new NotesV3Exception(404, "Không tìm thấy ghi chú trang PDF.");

        if (!page.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }

        page.ContentDeltaJson = deltaJson;

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict();
        }
    }

    private Task<NotesV3PdfPageLink?> FindLinkAsync(
        long versionId, int pageNumber, long? shareId,
        CancellationToken ct)
    {
        long? ownerId = shareId.HasValue ? null : currentUser.UserId;

        return repository.Query<NotesV3PdfPageLink>()
            .SingleOrDefaultAsync(x =>
                x.DocumentVersionId == versionId &&
                x.PhysicalPageNumber == pageNumber &&
                x.OwnerUserId == ownerId &&
                x.GroupDocumentShareId == shareId, ct);
    }

    private async Task<NotesV3Page> GetPageAsync(long pageId, CancellationToken ct)
    {
        return await repository.Query<NotesV3Page>()
            .SingleOrDefaultAsync(x => x.PageId == pageId &&
                                       x.SectionId == null &&
                                       !x.IsDeleted, ct)
            ?? throw new NotesV3Exception(404, "Không tìm thấy ghi chú trang PDF.");
    }

    private async Task RequirePageNumberAsync(
        long versionId, int pageNumber, CancellationToken ct)
    {
        var version = await repository.Query<NotesV3DocumentVersion>()
            .SingleOrDefaultAsync(x => x.DocumentVersionId == versionId, ct);

        if (version is null)
        {
            throw new NotesV3Exception(404, "Không tìm thấy phiên bản PDF.");
        }

        if (version.PageCount is not int count || count < 1)
        {
            throw new NotesV3Exception(409, "PDF chưa có số trang hợp lệ.");
        }

        if (pageNumber < 1 || pageNumber > count)
        {
            throw new NotesV3Exception(400, "Trang nằm ngoài tài liệu PDF.");
        }
    }

    private static bool IsUniqueConflict(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static NotesV3Exception Conflict() =>
        new(409, "Ghi chú đã thay đổi. Hãy tải lại trước khi lưu.");
}
