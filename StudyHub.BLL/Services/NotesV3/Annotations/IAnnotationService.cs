using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.Annotations;

public interface IAnnotationService
{
    Task<IReadOnlyList<PdfAnnotationItem>> ListAsync(
        long documentVersionId,
        int physicalPageNumber,
        long? groupDocumentShareId = null,
        CancellationToken ct = default);

    Task<PdfAnnotationItem> CreateAsync(
        long documentVersionId,
        int physicalPageNumber,
        PdfAnnotationPayload payload,
        long? groupDocumentShareId = null,
        CancellationToken ct = default);

    Task<PdfAnnotationItem> UpdateAsync(
        long annotationId,
        PdfAnnotationPayload payload,
        string versionToken,
        CancellationToken ct = default);

    Task DeleteAsync(
        long annotationId,
        string versionToken,
        CancellationToken ct = default);
}