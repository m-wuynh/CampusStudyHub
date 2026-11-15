namespace StudyHub.BLL.DTOs.Pdf;

public sealed record PdfDetailsDto(
    long DocumentId,
    string Title,
    string FileName,
    int PageCount,
    long? GroupId,
    string VersionToken);

public sealed record PdfPageNoteDto(
    string Text,
    string? VersionToken);
