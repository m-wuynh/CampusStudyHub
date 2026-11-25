using StudyHub.BLL.Services.Pdf;

namespace StudyHub.BLL.Services.Pdf.Documents;

internal static class PdfInputRules
{
    public static string FileName(string? value)
    {
        var fileName = Path.GetFileName(value?.Trim());
        var invalidName = string.IsNullOrWhiteSpace(fileName) ||
                          fileName.Length > PdfPolicy.MaxFileNameLength ||
                          !fileName.EndsWith(
                              ".pdf",
                              StringComparison.OrdinalIgnoreCase);

        if (invalidName)
        {
            throw new PdfException(
                400,
                $"Chỉ nhận file .pdf, tên tối đa " +
                $"{PdfPolicy.MaxFileNameLength} ký tự.");
        }

        return fileName!;
    }

    public static string Title(string? value, string fallback)
    {
        var title = string.IsNullOrWhiteSpace(value)
            ? fallback.Trim()
            : value.Trim();

        if (title.Length is < 1 or > PdfPolicy.MaxTitleLength)
        {
            throw new PdfException(
                400,
                $"Tiêu đề phải có từ 1 đến " +
                $"{PdfPolicy.MaxTitleLength} ký tự.");
        }

        return title;
    }

    public static string Note(string? value)
    {
        var note = (value ?? string.Empty).Trim();

        if (note.Length > PdfPolicy.MaxNoteLength)
        {
            throw new PdfException(
                400,
                $"Ghi chú tối đa {PdfPolicy.MaxNoteLength:N0} ký tự.");
        }

        return note;
    }
}
