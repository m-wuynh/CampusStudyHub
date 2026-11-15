using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Pdf;

public sealed class PdfLibraryService(
    IRepository repository,
    ICurrentUser currentUser)
{
    private const int PageSize = 12;

    public async Task<PdfTrashDto> GetTrashAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        var nowUtc = DateTime.UtcNow;
        var cutoffUtc = nowUtc.AddDays(-PdfTrashPolicy.RecoveryDays);
        var rows = await repository.Query<PdfDocument>()
            .Where(document => document.OwnerUserId == userId &&
                               document.IsDeleted &&
                               document.DeletedAtUtc > cutoffUtc)
            .OrderByDescending(document => document.UpdatedAtUtc)
            .Select(document => new
            {
                document.DocumentId,
                document.Title,
                document.DeletedAtUtc,
                document.RowVersion,
                document.OriginalFileName
            }).ToListAsync(ct);

        return new PdfTrashDto(rows.Select(row => new TrashedPdfDto(
            row.DocumentId,
            row.Title,
            row.OriginalFileName,
            Convert.ToBase64String(row.RowVersion),
            PdfTrashPolicy.DaysLeft(row.DeletedAtUtc, nowUtc))).ToList());
    }

    public async Task<PdfLibraryDto> GetAsync(
        string? searchQuery,
        int pageNumber,
        CancellationToken ct = default)
    {
        var userId = RequireUser();
        var queryText = NormalizeSearchQuery(searchQuery);
        var ownedDocuments = repository.Query<PdfDocument>()
            .Where(document => document.OwnerUserId == userId && !document.IsDeleted);
        var pdfCount = await ownedDocuments.CountAsync(ct);

        var noteMatches = queryText is null
            ? []
            : await (
                from document in ownedDocuments
                join note in repository.Query<PdfDocumentPageNote>()
                    on document.DocumentId equals note.DocumentId
                where note.OwnerUserId == userId &&
                      note.ContentText.Contains(queryText)
                group note by document.DocumentId into matches
                select new SearchHit(
                    matches.Key,
                    matches.Min(note => note.PhysicalPageNumber)))
                .ToListAsync(ct);

        var matchingDocumentIds = noteMatches
            .Select(match => match.DocumentId)
            .ToList();
        var documentQuery = ownedDocuments;
        if (queryText is not null)
        {
            documentQuery = documentQuery.Where(document =>
                document.Title.Contains(queryText) ||
                document.OriginalFileName.Contains(queryText) ||
                matchingDocumentIds.Contains(document.DocumentId));
        }

        var filteredCount = await documentQuery.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)PageSize));
        pageNumber = Math.Clamp(pageNumber, 1, totalPages);
        var rows = await documentQuery
            .OrderByDescending(document => document.UpdatedAtUtc)
            .Select(document => new
            {
                document.DocumentId,
                document.Title,
                document.RowVersion,
                document.OriginalFileName,
                document.PageCount
            })
            .Skip((pageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        var pdfs = rows.Select(row => new PdfSummaryDto(
            row.DocumentId,
            row.Title,
            row.OriginalFileName,
            row.PageCount,
            Convert.ToBase64String(row.RowVersion),
            noteMatches.Where(match => match.DocumentId == row.DocumentId)
                .Select(match => (int?)match.PageNumber).Min())).ToList();

        return new PdfLibraryDto(
            pdfCount,
            queryText ?? string.Empty,
            pageNumber,
            totalPages,
            pdfs);
    }

    private long RequireUser() => currentUser.UserId > 0
        ? currentUser.UserId
        : throw new PdfException(401, "Bạn cần đăng nhập.");

    private static string? NormalizeSearchQuery(string? value)
    {
        var result = value?.Trim();
        if (string.IsNullOrEmpty(result)) return null;
        return result.Length <= 100
            ? result
            : throw new PdfException(400, "Từ khóa tìm kiếm tối đa 100 ký tự.");
    }

    private sealed record SearchHit(long DocumentId, int PageNumber);
}
