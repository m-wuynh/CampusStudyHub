namespace StudyHub.BLL.DTOs.NotesV3;

public sealed record PdfPageNoteDetails(
    long DocumentVersionId,
    int PhysicalPageNumber,
    int PageCount,
    long? PageId,
    string? DeltaJson,
    string? VersionToken,
    bool IsGroup,
    bool IsReadOnly);

public sealed record SavePdfPageNoteRequest(
    string DeltaJson,
    string? VersionToken);

public sealed record SavePdfPageNoteResult(
    long? PageId,
    string? VersionToken,
    bool HasMeaningfulText);

public sealed record NotesBookPage(
    long PageId,
    int PhysicalPageNumber,
    string? DeltaJson,
    bool RequiresReplay);