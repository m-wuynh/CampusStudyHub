namespace StudyHub.Web.ViewModels.NotesV3;
// Màn 3: PDF và ghi chú của trang đang mở
public sealed record NotesReaderViewModel(
    long DocumentId,
    long VersionId,
    string Title,
    string FileName,
    int PageCount,
    int CurrentPage,
    string NoteText,
    string? NoteVersionToken,
    bool IsArchived,
    long? ShareId,
    IReadOnlyList<PdfMarkViewModel> Marks);

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
