using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.Pdfs;

public interface IPdfService
{
    Task<PdfDetailsDto> GetAsync(
        long documentId, long? shareId = null, CancellationToken ct = default);

    Task<PdfDownloadDto> OpenFileAsync(
        long versionId, long? shareId = null, CancellationToken ct = default);

    Task<PdfDetailsDto> UploadAsync(
        Stream content, string fileName, string? title,
        CancellationToken ct = default);

    Task RenameAsync(
        long documentId, string title, string versionToken,
        CancellationToken ct = default);

    Task SetArchivedAsync(
        long documentId, bool archived, string versionToken,
        CancellationToken ct = default);

    Task DeleteAsync(
        long documentId, string versionToken,
        CancellationToken ct = default);

    Task RestoreAsync(
        long documentId, string versionToken,
        CancellationToken ct = default);
}
