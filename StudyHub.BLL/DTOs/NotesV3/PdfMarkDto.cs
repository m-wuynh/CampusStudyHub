namespace StudyHub.BLL.DTOs.NotesV3;

public sealed record MarkPoint(double X, double Y);

public sealed record PdfMarkInput(
    string Kind,
    string Color,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<MarkPoint>? Points,
    string? Text = null);

public sealed record PdfMarkDto(
    long Id,
    PdfMarkInput Mark,
    string VersionToken);
