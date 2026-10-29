using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.DAL.Entities.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.References;

public interface IReferenceService
{
    Task<PersonalCaptureResult> CapturePersonalAsync(
        long pageId,
        PersonalCaptureRequest request,
        CancellationToken ct = default);

    Task<PersonalCaptureResult> CapturePersonalPdfAsync(
        long documentVersionId,
        int physicalPageNumber,
        PersonalCaptureRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReferenceItem>> ListAsync(
        long pageId,
        CancellationToken ct = default);

    Task<ReferenceDestination> ResolveAsync(
        long referenceId,
        CancellationToken ct = default);

    // Chỉ kiểm tra và tạo entity.
    // Service điều phối sẽ Add/Save trong transaction.
    Task<NotesV3Reference> PrepareCaptureAsync(
        long pageId,
        CaptureReferenceInput input,
        CancellationToken ct = default);
}