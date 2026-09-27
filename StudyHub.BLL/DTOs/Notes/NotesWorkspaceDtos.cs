using System.ComponentModel.DataAnnotations;

namespace StudyHub.BLL.DTOs.Notes;

public sealed record NotesUserDto(
    long Id,
    string Name,
    bool IsDemo);

public sealed record TextNoteDto(
    long Id,
    string Title,
    string Body,
    string Format,
    string Version,
    bool IsPinned);

public sealed record PdfNoteDto(
    long Id,
    string Title,
    long Size,
    bool IsPinned);

public sealed record NotesWorkspaceDto(
    List<TextNoteDto> Notes,
    List<PdfNoteDto> Documents);

public sealed class EditTextNoteDto
{
    public long? Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    [StringLength(200000)]
    public string Body { get; set; } = "";

    public string Format { get; set; } = "PlainText";

    public string? Version { get; set; }
}

public sealed record InkPoint(double X, double Y);

public sealed class InkStroke
{
    public string Tool { get; set; } = "pen";

    public string Color { get; set; } = "#2563eb";

    public double Width { get; set; } = 3;

    public List<InkPoint> Points { get; set; } = [];
}

public sealed class PdfPageNoteDto
{
    public Guid? Version { get; set; }

    public string Text { get; set; } = "";

    public List<InkStroke> Strokes { get; set; } = [];
}
