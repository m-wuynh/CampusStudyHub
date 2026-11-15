using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.Annotations;

public interface IPdfMarksService
{
    Task<IReadOnlyList<PdfMarkDto>> ListAsync(
        long versionId, int pageNumber, long? shareId,
        CancellationToken ct = default);

    Task CreateManyAsync(
        long versionId, int pageNumber, long? shareId,
        IReadOnlyList<PdfMarkInput> inputs, CancellationToken ct = default);

    Task DeleteAsync(
        long markId, long versionId, int pageNumber, long? shareId,
        string versionToken, CancellationToken ct = default);
}
