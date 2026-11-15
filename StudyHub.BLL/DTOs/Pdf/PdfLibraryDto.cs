namespace StudyHub.BLL.DTOs.Pdf;

public sealed record PdfLibraryDto(
    int PdfCount,
    string SearchQuery,
    int PageNumber,
    int TotalPages,
    IReadOnlyList<PdfSummaryDto> Pdfs);

public sealed record PdfTrashDto(
    IReadOnlyList<TrashedPdfDto> Pdfs);

public sealed record TrashedPdfDto(
    long Id,
    string Title,
    string FileName,
    string VersionToken,
    int DaysLeft);

public sealed record PdfSummaryDto(
    long Id,
    string Title,
    string FileName,
    int PageCount,
    string VersionToken,
    int? SearchPage);
