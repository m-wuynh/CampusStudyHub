using System.Text;

namespace StudyHub.BLL.Services.Pdf;

public static class PdfValidation
{
    public static string PdfFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PdfException(400, "Thiếu tên file PDF.");
        }

        var name = value
            .Replace('\\', '/')
            .Split('/')
            .Last()
            .Trim();

        if (name.Length <= 4 ||
            name.Length > 200 ||
            name.Any(char.IsControl) ||
            !name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfException(
                400,
                "Tên file phải có đuôi .pdf và tối đa 200 ký tự.");
        }

        return name;
    }

    public static string Title(string? value)
    {
        var title = value?.Trim();

        if (string.IsNullOrWhiteSpace(title) ||
            title.Length > 200 ||
            title.Any(char.IsControl))
        {
            throw new PdfException(
                400, "Tiêu đề phải có từ 1 đến 200 ký tự.");
        }

        return title;
    }

    public static byte[] Version(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PdfException(
                400, "Thiếu phiên bản dữ liệu.");
        }

        try
        {
            var bytes = Convert.FromBase64String(value);

            if (bytes.Length != 8)
            {
                throw new FormatException();
            }

            return bytes;
        }
        catch (FormatException)
        {
            throw new PdfException(
                400, "Phiên bản dữ liệu không hợp lệ.");
        }
    }

    public static string PdfNoteText(string? value)
    {
        var text = (value ?? string.Empty).Replace("\r\n", "\n");
        const int maxNoteBytes = 100 * 1024;
        if (Encoding.UTF8.GetByteCount(text) > maxNoteBytes)
        {
            throw new PdfException(413, "Ghi chú tối đa 100 KiB mỗi trang.");
        }

        return text;
    }
}
