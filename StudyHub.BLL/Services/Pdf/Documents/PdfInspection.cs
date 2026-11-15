using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

namespace StudyHub.BLL.Services.Pdf.Documents;

internal static class PdfInspection
{
    public static int CountPages(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        // Storage trả FileStream có thể seek.
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new InvalidOperationException(
                "PDF inspection requires a readable, seekable stream.");
        }

        if (stream.Length <= 0 || stream.Length > PdfPolicy.MaxFileBytes)
        {
            throw new PdfException(
                400, "Kích thước file PDF không hợp lệ.");
        }

        var originalPosition = stream.Position;

        try
        {
            stream.Position = 0;

            // UseLenientParsing = false: từ chối PDF cấu trúc lỗi ngay từ đầu.
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
                throw new PdfException(
                    400, "PDF phải có ít nhất một trang.");
            }

            if (pageCount > PdfPolicy.MaxPageCount)
            {
                throw new PdfException(
                    400, $"PDF tối đa {PdfPolicy.MaxPageCount:N0} trang.");
            }

            // Chỉ validate MediaBox của trang đầu và trang cuối thay vì
            // duyệt toàn bộ. PdfPig đọc page tree từ cross-reference table
            // nên GetPage() ở đây chỉ parse MediaBox, không parse content
            // operators. Kiểm tra 2 trang đại diện đủ để phát hiện PDF có
            // cấu trúc trang bất thường mà không gây blocking O(n) trên
            // upload thread khi tài liệu lớn.
            cancellationToken.ThrowIfCancellationRequested();
            ValidatePageDimensions(document.GetPage(1), 1);

            if (pageCount > 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidatePageDimensions(document.GetPage(pageCount), pageCount);
            }

            return pageCount;
        }
        catch (PdfDocumentEncryptedException)
        {
            throw ProtectedPdf();
        }
        catch (PdfDocumentFormatException)
        {
            throw new PdfException(
                400,
                "PDF bị lỗi hoặc có cấu trúc không được hỗ trợ.");
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static void ValidatePageDimensions(Page page, int pageNumber)
    {
        if (!double.IsFinite(page.Width) ||
            !double.IsFinite(page.Height) ||
            page.Width <= 0 ||
            page.Height <= 0)
        {
            throw new PdfException(
                400,
                $"Trang PDF {pageNumber} có kích thước không hợp lệ.");
        }
    }

    private static PdfException ProtectedPdf() =>
        new(
            400,
            "Chưa hỗ trợ PDF được mã hóa hoặc bảo vệ bằng mật khẩu.");
}
