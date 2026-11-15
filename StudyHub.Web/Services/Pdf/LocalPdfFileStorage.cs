using System.Buffers;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Documents;

namespace StudyHub.Web.Services.Pdf;

public sealed class LocalPdfFileStorage(
    IWebHostEnvironment environment) : IPdfFileStorage
{
    private const int BufferSize = 64 * 1024;

    private string RootPath => Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "Pdf");

    public async Task<StoredPdf> SaveAsync(
        Stream source,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
        {
            throw new PdfException(
                400, "Không thể đọc file tải lên.");
        }

        var safeFileName = PdfValidation.PdfFileName(fileName);

        var header = new byte[5];
        var headerLength = 0;

        while (headerLength < header.Length)
        {
            var read = await source.ReadAsync(
                header.AsMemory(headerLength),
                cancellationToken);

            if (read == 0)
            {
                break;
            }

            headerLength += read;
        }

        if (headerLength != header.Length ||
            !header.AsSpan().SequenceEqual("%PDF-"u8))
        {
            throw new PdfException(
                400, "File không có chữ ký PDF hợp lệ.");
        }

        Directory.CreateDirectory(RootPath);

        var storageKey = $"{Guid.NewGuid():N}.pdf";
        var finalPath = ResolvePath(storageKey);
        var temporaryPath = finalPath + ".upload";

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var completed = false;

        try
        {
            long totalBytes = header.Length;

            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan))
            {
                await destination.WriteAsync(
                    header.AsMemory(),
                    cancellationToken);

                while (true)
                {
                    var read = await source.ReadAsync(
                        buffer.AsMemory(0, BufferSize),
                        cancellationToken);

                    if (read == 0)
                    {
                        break;
                    }

                    totalBytes += read;

                    if (totalBytes > PdfPolicy.MaxFileBytes)
                    {
                        throw new PdfException(
                            413, "PDF tối đa 25 MiB.");
                    }

                    await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken);
                }

                await destination.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // File hoàn chỉnh mới được chuyển sang tên chính thức.
            // Không ghi đè file PDF đã tồn tại.
            File.Move(
                temporaryPath,
                finalPath,
                overwrite: false);

            completed = true;

            return new StoredPdf(
                storageKey,
                safeFileName,
                totalBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            if (!completed)
            {
                TryDeleteTemporary(temporaryPath);
            }
        }
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolvePath(storageKey);

        try
        {
            Stream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

            return Task.FromResult(stream);
        }
        catch (FileNotFoundException)
        {
            throw MissingFile();
        }
        catch (DirectoryNotFoundException)
        {
            throw MissingFile();
        }
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolvePath(storageKey);

        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // File đã không còn: thao tác dọn dẹp được xem là hoàn tất.
        }

        return Task.CompletedTask;
    }

    public Task<int> DeleteOrphansAsync(
        IReadOnlyCollection<string> retainedStorageKeys,
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(retainedStorageKeys);

        if (!Directory.Exists(RootPath))
        {
            return Task.FromResult(0);
        }

        var retained = retainedStorageKeys.ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        var deletedCount = 0;

        foreach (var path in Directory.EnumerateFiles(
            RootPath,
            "*",
            SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = new FileInfo(path);
            var isPdf = file.Name.EndsWith(
                ".pdf", StringComparison.OrdinalIgnoreCase);
            var isInterruptedUpload = file.Name.EndsWith(
                ".pdf.upload", StringComparison.OrdinalIgnoreCase);
            if ((!isPdf && !isInterruptedUpload) ||
                file.LastWriteTimeUtc > olderThanUtc ||
                (isPdf && retained.Contains(file.Name)))
            {
                continue;
            }

            try
            {
                file.Delete();
                deletedCount++;
            }
            catch (FileNotFoundException)
            {
                // Một tiến trình dọn dẹp khác đã xóa file.
            }
        }

        return Task.FromResult(deletedCount);
    }

    private string ResolvePath(string storageKey)
    {
        // Chỉ chấp nhận key do storage này tạo.
        // Không nhận đường dẫn hoặc tên file từ client.
        if (string.IsNullOrWhiteSpace(storageKey) ||
            storageKey.Length != 36 ||
            !storageKey.EndsWith(
                ".pdf", StringComparison.Ordinal) ||
            !Guid.TryParseExact(
                storageKey[..32],
                "N",
                out _))
        {
            throw MissingFile();
        }

        return Path.Combine(RootPath, storageKey);
    }

    private static void TryDeleteTemporary(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Không che mất lỗi upload ban đầu.
        }
        catch (UnauthorizedAccessException)
        {
            // Không che mất lỗi upload ban đầu.
        }
    }

    private static PdfException MissingFile() =>
        new(404, "Không tìm thấy file PDF.");
}
