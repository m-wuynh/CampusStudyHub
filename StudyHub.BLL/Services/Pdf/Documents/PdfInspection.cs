using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;
using StudyHub.BLL.Services.Pdf;

namespace StudyHub.BLL.Services.Pdf.Documents;

internal static class PdfInspection
{
    public static int CountPages(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new PdfException(400,
                "PDF stream không thể đọc.");
        }

        if (stream.Length <= 0 ||
            stream.Length > PdfPolicy.MaxFileBytes)
        {
            throw new PdfException(400,
                "Kích thước PDF không hợp lệ.");
        }

        var position = stream.Position;
        try
        {
            stream.Position = 0;
            using var document = PdfDocument.Open(stream);

            if (document.IsEncrypted)
            {
                throw new PdfException(400,
                    "Chưa hỗ trợ PDF có mật khẩu.");
            }

            var pages = document.NumberOfPages;
            if (pages < 1 || pages > PdfPolicy.MaxPageCount)
            {
                throw new PdfException(400,
                    $"PDF phải có từ 1 đến {PdfPolicy.MaxPageCount:N0} trang.");
            }

            return pages;
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new PdfException(400,
                "Chưa hỗ trợ PDF có mật khẩu.");
        }
        catch (PdfDocumentFormatException)
        {
            throw new PdfException(400,
                "PDF bị lỗi hoặc không được hỗ trợ.");
        }
        finally
        {
            stream.Position = position;
        }
    }
}
