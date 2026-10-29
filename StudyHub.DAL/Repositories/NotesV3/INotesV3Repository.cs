using StudyHub.DAL.Entities.NotesV3;

namespace StudyHub.DAL.Repositories.NotesV3;

public interface INotesV3Repository
{
    Task<NotesV3PdfPageLink?> FindPdfPageLinkAsync(
        long documentVersionId,
        int physicalPageNumber,
        long? ownerUserId,
        long? groupDocumentShareId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotesV3CollabUpdate>> ReadUpdatesAsync(
        long pageId,
        long afterUpdateId,
        int take,
        CancellationToken cancellationToken = default);

    Task<NotesV3CollabUpdate?> FindOperationAsync(
        long pageId,
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<long> GetStoredUpdateBytesAsync(
        long pageId,
        CancellationToken cancellationToken = default);
    Task AcquireLockAsync(
    string resource,
    CancellationToken cancellationToken = default);
}