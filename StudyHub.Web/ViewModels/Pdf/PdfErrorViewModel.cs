namespace StudyHub.Web.ViewModels.Pdf;

public sealed record PdfErrorViewModel(
    int StatusCode,
    string Message);
