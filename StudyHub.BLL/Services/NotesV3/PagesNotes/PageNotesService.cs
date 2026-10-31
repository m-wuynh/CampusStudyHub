using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.PageNotes;

public sealed class PageNotesService(
    IRepository repository,
    ICurrentUser currentUser,
    INotesV3AccessService access) : IPageNotesService
{
    public async Task<PdfPageNoteDetails> GetAsync(
        long documentVersionId,
        int physicalPageNumber,
        long? groupDocumentShareId = null,
        CancellationToken ct = default)
    {
        var document = await access.RequirePdfAsync(
            documentVersionId,
            groupDocumentShareId,
            cancellationToken: ct);

        var pageCount = await RequirePageNumberAsync(
            documentVersionId,
            physicalPageNumber,
            ct);

        var link = await FindLinkAsync(
            documentVersionId,
            physicalPageNumber,
            groupDocumentShareId,
            ct);

        var isGroup = groupDocumentShareId.HasValue;

        if (link is null)
        {
            // Điều hướng không tạo Page hoặc PdfPageLink.
            return new PdfPageNoteDetails(
                documentVersionId,
                physicalPageNumber,
                pageCount,
                null,
                isGroup ? null : NotesV3Validation.EmptyDelta,
                null,
                isGroup,
                document.IsArchived);
        }

        var page = await RequireLinkedPageAsync(link.PageId, ct);

        return new PdfPageNoteDetails(
            documentVersionId,
            physicalPageNumber,
            pageCount,
            page.PageId,
            isGroup ? null : page.ContentDeltaJson,
            Convert.ToBase64String(page.RowVersion),
            isGroup,
            document.IsArchived);
    }

    public async Task<SavePdfPageNoteResult> SavePersonalAsync(
        long documentVersionId,
        int physicalPageNumber,
        SavePdfPageNoteRequest request,
        CancellationToken ct = default)
    {
        await access.RequirePdfAsync(
            documentVersionId,
            forWrite: true,
            cancellationToken: ct);

        await RequirePageNumberAsync(
            documentVersionId,
            physicalPageNumber,
            ct);

        var delta = NotesV3Validation.Delta(request.DeltaJson);

        var link = await FindLinkAsync(
            documentVersionId,
            physicalPageNumber,
            groupDocumentShareId: null,
            ct: ct);

        if (link is not null)
        {
            return await UpdatePersonalAsync(
                link.PageId,
                request.VersionToken,
                delta,
                ct);
        }

        if (!string.IsNullOrWhiteSpace(request.VersionToken))
        {
            throw Conflict();
        }

        if (!delta.HasMeaningfulText)
        {
            // Không tạo ghi chú mới cho nội dung rỗng.
            return new SavePdfPageNoteResult(
                null,
                null,
                false);
        }

        await using var transaction =
            await repository.BeginTransactionAsync(ct);

        try
        {
            var page = new NotesV3Page
            {
                SectionId = null,
                Title = $"Trang {physicalPageNumber}",
                ContentDeltaJson = delta.Json
            };

            await repository.AddAsync(page, ct);
            await repository.SaveChangesAsync(ct);

            await repository.AddAsync(
                new NotesV3PdfPageLink
                {
                    PageId = page.PageId,
                    DocumentVersionId = documentVersionId,
                    OwnerUserId = currentUser.UserId,
                    GroupDocumentShareId = null,
                    PhysicalPageNumber = physicalPageNumber
                },
                ct);

            await repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new SavePdfPageNoteResult(
                page.PageId,
                Convert.ToBase64String(page.RowVersion),
                true);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConflict(exception))
        {
            // Hai tab cùng tạo ghi chú cho một trang.
            // Transaction sẽ rollback khi thoát method.
            // Client giữ draft và tải lại bản đã được tạo.
            throw Conflict();
        }
    }

    public async Task<IReadOnlyList<NotesBookPage>> ListBookPagesAsync(
        long documentVersionId,
        long? groupDocumentShareId = null,
        CancellationToken ct = default)
    {
        await access.RequirePdfAsync(
            documentVersionId,
            groupDocumentShareId,
            cancellationToken: ct);

        long? ownerUserId = groupDocumentShareId.HasValue
            ? null
            : currentUser.UserId;

        // Tạo query roots bên ngoài expression tree.
        var links = repository.Query<NotesV3PdfPageLink>();
        var pages = repository.Query<NotesV3Page>();

        var query =
            from link in links
            join page in pages
                on link.PageId equals page.PageId
            where link.DocumentVersionId == documentVersionId
                  && link.OwnerUserId == ownerUserId
                  && link.GroupDocumentShareId == groupDocumentShareId
                  && !page.IsDeleted
                  && page.SectionId == null
            orderby link.PhysicalPageNumber
            select new
            {
                page.PageId,
                link.PhysicalPageNumber,
                page.ContentDeltaJson
            };

        if (groupDocumentShareId.HasValue)
        {
            // Không đọc Delta làm nội dung chính của trang nhóm.
            var candidates = await query
                .Select(item => new
                {
                    item.PageId,
                    item.PhysicalPageNumber
                })
                .ToListAsync(ct);

            return candidates
                .Select(item => new NotesBookPage(
                    item.PageId,
                    item.PhysicalPageNumber,
                    null,
                    RequiresReplay: true))
                .ToList();
        }

        var personalPages = await query.ToListAsync(ct);
        var result = new List<NotesBookPage>();

        foreach (var item in personalPages)
        {
            var delta = NotesV3Validation.Delta(
                item.ContentDeltaJson);

            if (!delta.HasMeaningfulText)
            {
                continue;
            }

            result.Add(new NotesBookPage(
                item.PageId,
                item.PhysicalPageNumber,
                delta.Json,
                RequiresReplay: false));
        }

        return result;
    }

    private async Task<SavePdfPageNoteResult> UpdatePersonalAsync(
        long pageId,
        string? versionToken,
        ValidatedDelta delta,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(versionToken))
        {
            throw Conflict();
        }

        var expected = NotesV3Validation.Version(versionToken);

        var page = await repository
            .Query<NotesV3Page>(asNoTracking: false)
            .SingleOrDefaultAsync(
                item => item.PageId == pageId &&
                        item.SectionId == null &&
                        !item.IsDeleted,
                ct)
            ?? throw Missing();

        if (!page.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }

        // Nội dung rỗng vẫn được lưu trên row đã có.
        // Notes Book sẽ lọc row này, không tạo bản sao riêng.
        page.ContentDeltaJson = delta.Json;

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict();
        }

        return new SavePdfPageNoteResult(
            page.PageId,
            Convert.ToBase64String(page.RowVersion),
            delta.HasMeaningfulText);
    }

    private Task<NotesV3PdfPageLink?> FindLinkAsync(
        long documentVersionId,
        int physicalPageNumber,
        long? groupDocumentShareId,
        CancellationToken ct)
    {
        long? ownerUserId = groupDocumentShareId.HasValue
            ? null
            : currentUser.UserId;

        return repository.NotesV3.FindPdfPageLinkAsync(
            documentVersionId,
            physicalPageNumber,
            ownerUserId,
            groupDocumentShareId,
            ct);
    }

    private async Task<int> RequirePageNumberAsync(
        long documentVersionId,
        int physicalPageNumber,
        CancellationToken ct)
    {
        if (physicalPageNumber < 1)
        {
            throw new NotesV3Exception(
                400, "Số trang PDF bắt đầu từ 1.");
        }

        var version = await repository
            .Query<NotesV3DocumentVersion>()
            .SingleOrDefaultAsync(
                item => item.DocumentVersionId == documentVersionId,
                ct)
            ?? throw Missing();

        if (version.PageCount is not int count || count < 1)
        {
            throw new NotesV3Exception(
                409,
                "PDF chưa được xác định số trang trên server.");
        }

        if (physicalPageNumber > count)
        {
            throw new NotesV3Exception(
                400, "Trang nằm ngoài tài liệu PDF.");
        }

        return count;
    }

    private async Task<NotesV3Page> RequireLinkedPageAsync(
        long pageId,
        CancellationToken ct)
    {
        return await repository
            .Query<NotesV3Page>()
            .SingleOrDefaultAsync(
                item => item.PageId == pageId &&
                        item.SectionId == null &&
                        !item.IsDeleted,
                ct)
            ?? throw Missing();
    }

    private static bool IsUniqueConflict(
        DbUpdateException exception) =>
        exception.InnerException is SqlException
        {
            Number: 2601 or 2627
        };

    private static NotesV3Exception Missing() =>
        new(404, "Không tìm thấy ghi chú trang PDF.");

    private static NotesV3Exception Conflict() =>
        new(
            409,
            "Ghi chú đã thay đổi ở nơi khác. Giữ bản nháp và tải lại trước khi lưu.");
}