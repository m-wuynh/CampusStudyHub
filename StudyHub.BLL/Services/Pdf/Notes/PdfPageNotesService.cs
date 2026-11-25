using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Pdf.Access;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Pdf.Notes;

public sealed class PdfPageNotesService(
    IRepository repository,
    ICurrentUser currentUser,
    PdfAccessService access)
{
    public async Task<PdfPageNoteDto> GetTextAsync(
        long documentId, int pageNumber, long? shareId,
        CancellationToken ct = default)
    {
        await access.RequirePdfPageAsync(
            documentId, pageNumber, shareId, cancellationToken: ct);
        if (shareId.HasValue)
        {
            return new PdfPageNoteDto(string.Empty, null);
        }

        var note = await FindAsync(documentId, pageNumber, ct);
        return note is null
            ? new PdfPageNoteDto(string.Empty, null)
            : new PdfPageNoteDto(
                note.ContentText,
                Convert.ToBase64String(note.RowVersion));
    }

    public async Task<string?> SaveTextAsync(
        long documentId, int pageNumber, string text,
        string? versionToken, long? shareId,
        CancellationToken ct = default)
    {
        await access.RequirePdfPageAsync(
            documentId, pageNumber, shareId, forWrite: true, cancellationToken: ct);
        var normalizedText = PdfValidation.PdfNoteText(text);
        var note = await repository.Query<PdfDocumentPageNote>(false)
            .SingleOrDefaultAsync(x => x.DocumentId == documentId &&
                                       x.PhysicalPageNumber == pageNumber &&
                                       x.OwnerUserId == currentUser.UserId, ct);

        if (note is not null)
        {
            return await UpdateAsync(note, versionToken, normalizedText, ct);
        }

        if (!string.IsNullOrWhiteSpace(versionToken))
        {
            throw Conflict();
        }

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return null;
        }

        note = new PdfDocumentPageNote
        {
            DocumentId = documentId,
            OwnerUserId = currentUser.UserId,
            PhysicalPageNumber = pageNumber,
            ContentText = normalizedText
        };
        await repository.AddAsync(note, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
            return Convert.ToBase64String(note.RowVersion);
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            throw Conflict();
        }
    }

    private async Task<string?> UpdateAsync(
        PdfDocumentPageNote note,
        string? versionToken,
        string text,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(versionToken) ||
            !note.RowVersion.AsSpan().SequenceEqual(
                PdfValidation.Version(versionToken)))
        {
            throw Conflict();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            repository.Remove(note);
        }
        else
        {
            note.ContentText = text;
        }

        try
        {
            await repository.SaveChangesAsync(ct);
            return string.IsNullOrWhiteSpace(text)
                ? null
                : Convert.ToBase64String(note.RowVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict();
        }
    }

    private Task<PdfDocumentPageNote?> FindAsync(
        long documentId,
        int pageNumber,
        CancellationToken ct) =>
        repository.Query<PdfDocumentPageNote>()
            .SingleOrDefaultAsync(x => x.DocumentId == documentId &&
                                       x.PhysicalPageNumber == pageNumber &&
                                       x.OwnerUserId == currentUser.UserId, ct);

    private static bool IsUniqueConflict(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static PdfException Conflict() =>
        new(409, "Ghi chú đã thay đổi. Hãy tải lại trước khi lưu.");
}
