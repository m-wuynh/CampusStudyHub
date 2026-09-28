using StudyHub.BLL.DTOs.Notes;
namespace StudyHub.BLL.Services.Notes;

public interface INotesWorkspaceService
{
    Task<NotesWorkspaceDto> ListAsync(long userId, string? search, CancellationToken ct);
    Task<TextNoteDto> GetTextAsync(long userId, long id, CancellationToken ct);
    Task SaveTextAsync(long userId, EditTextNoteDto dto, CancellationToken ct);
    Task DeleteTextAsync(long userId, long id, string version, CancellationToken ct);
    Task<long> UploadAsync(long userId, string name, Stream input, CancellationToken ct);
    Task<PdfNoteDto> GetPdfAsync(long userId, long id, CancellationToken ct);
    Task RenamePdfAsync(long userId, long id, string title, CancellationToken ct);
    Task DeletePdfAsync(long userId, long id, CancellationToken ct);
    Task<Stream> OpenPdfAsync(long userId, long id, CancellationToken ct);
    Task<PdfPageNoteDto> GetPageAsync(long userId, long id, int page, CancellationToken ct);
    Task<PdfPageNoteDto> SavePageAsync(long userId, long id, int page, PdfPageNoteDto dto, CancellationToken ct);
    Task TogglePinAsync(
    long userId,
    long id,
    CancellationToken ct);
    Task TogglePdfPinAsync(
    long userId,
    long id,
    CancellationToken ct);
}
