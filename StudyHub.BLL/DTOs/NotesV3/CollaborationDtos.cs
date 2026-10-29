namespace StudyHub.BLL.DTOs.NotesV3;

public sealed record CollabUpdateRequest(
    long PageId,
    Guid OperationId,
    byte[] UpdateBytes,
    CaptureReferenceInput? Reference = null);

public sealed record PdfCollabUpdateRequest(
    long DocumentVersionId,
    long GroupDocumentShareId,
    int PhysicalPageNumber,
    Guid OperationId,
    byte[] UpdateBytes,
    string InitialDeltaJson,
    CaptureReferenceInput? Reference = null);

public sealed record CollabAck(
    long PageId,
    long UpdateId,
    Guid OperationId,
    bool AlreadyStored);

public sealed record CollabUpdateItem(
    long UpdateId,
    Guid OperationId,
    byte[] UpdateBytes);

public sealed record CollabBatch(
    long PageId,
    IReadOnlyList<CollabUpdateItem> Updates,
    long NextAfterId,
    bool HasMore);