using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.Collaboration;

public interface ICollaborationService
{
    Task AuthorizePageAsync(
        long pageId,
        CancellationToken ct = default);

    Task<CollabAck> AppendAsync(
        CollabUpdateRequest request,
        CancellationToken ct = default);

    // NULL: chưa có Page Note và nội dung ban đầu rỗng.
    Task<CollabAck?> AppendPdfPageAsync(
        PdfCollabUpdateRequest request,
        CancellationToken ct = default);

    Task<CollabBatch> ReadUpdatesAsync(
        long pageId,
        long afterUpdateId,
        CancellationToken ct = default);
}