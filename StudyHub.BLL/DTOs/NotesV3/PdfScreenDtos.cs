namespace StudyHub.BLL.DTOs.NotesV3;

public sealed record PdfDetailsDto(
    long DocumentId,
    long DocumentVersionId,
    string Title,
    string FileName,
    int PageCount,
    bool IsArchived,
    string VersionToken);

public sealed record PdfDownloadDto(
    Stream Content,
    string FileName);

public sealed record PdfPageNoteDto(
    string Text,
    string? VersionToken);

public sealed record PdfBookPageDto(
    int PageNumber,
    string Text);
