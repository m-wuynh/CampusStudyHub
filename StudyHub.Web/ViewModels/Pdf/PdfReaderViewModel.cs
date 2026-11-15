namespace StudyHub.Web.ViewModels.Pdf;

public sealed record PdfReaderViewModel(
    long DocumentId,
    string Title,
    string FileName,
    int PageCount,
    int CurrentPage,
    string NoteText,
    string? NoteVersionToken,
    long? ShareId,
    long? GroupId,
    IReadOnlyList<PdfMarkViewModel> Marks)
{
    public bool IsReadOnly => ShareId.HasValue;
}

public sealed record MarkPointViewModel(double X, double Y);

public sealed record PdfMarkViewModel(
    long Id,
    string Kind,
    string Color,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<MarkPointViewModel> Points,
    string VersionToken,
    string? Text);
