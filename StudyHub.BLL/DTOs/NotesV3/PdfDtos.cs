namespace StudyHub.BLL.DTOs.NotesV3;

public sealed record PdfItem(
    long DocumentId,
    long DocumentVersionId,
    string Title,
    string FileName,
    long SizeBytes,
    int? PageCount,
    bool IsArchived,
    string VersionToken,
    DateTime UpdatedAtUtc);

public sealed record PdfMutationResult(
    string VersionToken,
    DateTime UpdatedAtUtc);

public sealed record PdfDownload(
    Stream Content,
    string FileName,
    long SizeBytes);
public sealed record EligibleNotesGroup(
    long GroupId,
    string Name);

public sealed record PdfShareResult(
    long ShareId,
    long DocumentId,
    long GroupId,
    string VersionToken);

public sealed record GroupPdfItem(
    long ShareId,
    long DocumentId,
    long DocumentVersionId,
    string Title,
    string FileName,
    long SizeBytes,
    int? PageCount,
    bool CanUnshare,
    string ShareVersionToken);
public sealed record PdfAnnotationPayload(
    string Kind,
    double X,
    double Y,
    double Width,
    double Height,
    string? Text,
    string Color);

public sealed record PdfAnnotationItem(
    long AnnotationId,
    int PhysicalPageNumber,
    PdfAnnotationPayload Payload,
    string VersionToken);