namespace StudyHub.BLL.DTOs.NotesV3;

public sealed record ReferenceAnchor(
    double X,
    double Y,
    double Width,
    double Height);

public sealed record CaptureReferenceInput(
    long DocumentVersionId,
    int PhysicalPageNumber,
    long? GroupDocumentShareId,
    string CapturedText,
    ReferenceAnchor? Anchor);

public sealed record PersonalCaptureRequest(
    string DeltaJson,
    string? VersionToken,
    CaptureReferenceInput Source);

public sealed record PersonalCaptureResult(
    long ReferenceId,
    string VersionToken,
    long PageId);

public sealed record ReferenceItem(
    long ReferenceId,
    int PhysicalPageNumber,
    string CapturedText);

public sealed record ReferenceDestination(
    bool Available,
    long? DocumentVersionId,
    int? PhysicalPageNumber,
    long? GroupDocumentShareId,
    string? DocumentTitle,
    long? DocumentId = null);