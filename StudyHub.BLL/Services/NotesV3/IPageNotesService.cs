using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.PagesNotes;

public interface IPageNotesService
{
    Task<PdfPageNoteDto> GetTextAsync(
        long versionId, int pageNumber, long? shareId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PdfBookPageDto>> ListBookTextAsync(
        long versionId, long? shareId,
        CancellationToken ct = default);

    Task SaveTextAsync(
        long versionId, int pageNumber, string text,
        string? versionToken, long? shareId,
        CancellationToken ct = default);
}
