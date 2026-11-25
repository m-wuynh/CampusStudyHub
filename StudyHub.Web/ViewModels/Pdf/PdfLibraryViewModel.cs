namespace StudyHub.Web.ViewModels.Pdf;

public sealed record PdfLibraryViewModel(
    int PdfCount,
    string SearchQuery,
    int PageNumber,
    int TotalPages,
    IReadOnlyList<PdfCardViewModel> Pdfs);

public sealed record PdfTrashViewModel(
    IReadOnlyList<TrashedPdfViewModel> Pdfs);

public sealed record TrashedPdfViewModel(
    long Id,
    string Title,
    string FileName,
    string VersionToken,
    int DaysLeft);

public sealed record PdfCardViewModel(
    long Id,
    string Title,
    string FileName,
    int PageCount,
    string VersionToken,
    int? SearchPage);
