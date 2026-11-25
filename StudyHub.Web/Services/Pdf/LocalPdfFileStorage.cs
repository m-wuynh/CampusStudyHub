using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Documents;

namespace StudyHub.Web.Services.Pdf;

public sealed class LocalPdfFileStorage(
    IWebHostEnvironment environment) : IPdfFileStorage
{
    private const int BufferSize = 80 * 1024;

    private string RootPath => Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "Pdf");

    public async Task<StoredPdf> SaveAsync(
        Stream source,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!source.CanRead)
        {
            throw new PdfException(400, "Không thể đọc file tải lên.");
        }

        var header = await ReadAndValidateHeaderAsync(
            source,
            cancellationToken);

        Directory.CreateDirectory(RootPath);

        var storageKey = $"{Guid.NewGuid():N}.pdf";
        var finalPath = ResolvePath(storageKey);
        var temporaryPath = finalPath + ".upload";

        try
        {
            var size = await WriteTemporaryFileAsync(
                source,
                header,
                temporaryPath,
                cancellationToken);

            File.Move(temporaryPath, finalPath);
            return new StoredPdf(storageKey, fileName, size);
        }
        catch
        {
            DeleteIfExists(temporaryPath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Stream stream = File.Open(
                ResolvePath(storageKey),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            return Task.FromResult(stream);
        }
        catch (FileNotFoundException)
        {
            throw new PdfException(404, "Không tìm thấy file PDF.");
        }
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteIfExists(ResolvePath(storageKey));
        return Task.CompletedTask;
    }

    private static async Task<byte[]> ReadAndValidateHeaderAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        var header = new byte[5];

        try
        {
            await source.ReadExactlyAsync(header, cancellationToken);
        }
        catch (EndOfStreamException)
        {
            throw new PdfException(400, "File PDF không hợp lệ.");
        }

        if (!header.AsSpan().SequenceEqual("%PDF-"u8))
        {
            throw new PdfException(
                400,
                "File không có chữ ký PDF hợp lệ.");
        }

        return header;
    }

    private static async Task<long> WriteTemporaryFileAsync(
        Stream source,
        byte[] header,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            useAsync: true);

        await destination.WriteAsync(header, cancellationToken);

        var totalBytes = (long)header.Length;
        var buffer = new byte[BufferSize];

        while (true)
        {
            var bytesRead = await source.ReadAsync(
                buffer,
                cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
            if (totalBytes > PdfPolicy.MaxFileBytes)
            {
                throw new PdfException(413, "PDF tối đa 25 MiB.");
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);
        }

        await destination.FlushAsync(cancellationToken);
        return totalBytes;
    }

    private string ResolvePath(string storageKey)
    {
        var invalidKey = Path.GetFileName(storageKey) != storageKey ||
                         !storageKey.EndsWith(
                             ".pdf",
                             StringComparison.OrdinalIgnoreCase);

        if (invalidKey)
        {
            throw new PdfException(400, "Khóa lưu trữ không hợp lệ.");
        }

        return Path.Combine(RootPath, storageKey);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
