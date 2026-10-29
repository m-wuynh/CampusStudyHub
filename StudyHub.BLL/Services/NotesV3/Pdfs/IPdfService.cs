using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.Pdfs;

public interface IPdfService
{
    Task<IReadOnlyList<PdfItem>> ListAsync(
        bool includeArchived = false,
        CancellationToken ct = default);

    Task<PdfItem> UploadAsync(
        Stream content,
        string fileName,
        string? title,
        CancellationToken ct = default);

    Task<PdfItem> GetAsync(
        long documentId,
        long? groupDocumentShareId = null,
        CancellationToken ct = default);

    Task<PdfDownload> OpenFileAsync(
        long documentVersionId,
        long? groupDocumentShareId = null,
        CancellationToken ct = default);

    Task<PdfMutationResult> RenameAsync(
        long documentId,
        string title,
        string versionToken,
        CancellationToken ct = default);

    Task<PdfMutationResult> SetArchivedAsync(
        long documentId,
        bool archived,
        string versionToken,
        CancellationToken ct = default);
}