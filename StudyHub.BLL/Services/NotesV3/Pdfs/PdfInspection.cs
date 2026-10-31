using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

namespace StudyHub.BLL.Services.NotesV3.Pdfs;

public static class PdfInspection
{
    private const long MaxPdfBytes = 25L * 1024 * 1024;

    public static int ReadPageCount(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        // Storage mới trả FileStream có thể seek.
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new InvalidOperationException(
                "PDF inspection requires a readable, seekable stream.");
        }

        if (stream.Length <= 0 || stream.Length > MaxPdfBytes)
        {
            throw new NotesV3Exception(
                400, "Kích thước file PDF không hợp lệ.");
        }

        var originalPosition = stream.Position;

        try
        {
            stream.Position = 0;

            using var document = PdfDocument.Open(
                stream,
                new ParsingOptions
                {
                    UseLenientParsing = false
                });

            if (document.IsEncrypted)
            {
                throw ProtectedPdf();
            }

            var pageCount = document.NumberOfPages;

            if (pageCount < 1)
            {
                throw new NotesV3Exception(
                    400, "PDF phải có ít nhất một trang.");
            }

            // Parse từng trang để phát hiện lỗi nội dung mà việc
            // chỉ đọc header hoặc page count chưa phát hiện được.
            for (var pageNumber = 1;
                 pageNumber <= pageCount;
                 pageNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = document.GetPage(pageNumber);

                if (!double.IsFinite(page.Width) ||
                    !double.IsFinite(page.Height) ||
                    page.Width <= 0 ||
                    page.Height <= 0)
                {
                    throw new NotesV3Exception(
                        400,
                        $"Trang PDF {pageNumber} có kích thước không hợp lệ.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            return pageCount;
        }
        catch (PdfDocumentEncryptedException)
        {
            throw ProtectedPdf();
        }
        catch (PdfDocumentFormatException)
        {
            throw new NotesV3Exception(
                400,
                "PDF bị lỗi hoặc có cấu trúc không được hỗ trợ.");
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static NotesV3Exception ProtectedPdf() =>
        new(
            400,
            "Chưa hỗ trợ PDF được mã hóa hoặc bảo vệ bằng mật khẩu.");
}