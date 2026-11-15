using System.Buffers;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.BLL.Services.NotesV3.Pdfs;

namespace StudyHub.Web.Services.NotesV3;

public sealed class LocalPdfFileStorage(
    IWebHostEnvironment environment) : IPdfFileStorage
{
    private const long MaxPdfBytes = 25L * 1024 * 1024;
    private const int BufferSize = 64 * 1024;

    private string RootPath => Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "NotesV3",
        "Pdfs");

    public async Task<StoredPdf> SaveAsync(
        Stream source,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
        {
            throw new NotesV3Exception(
                400, "Không thể đọc file tải lên.");
        }

        var safeFileName = NormalizeFileName(fileName);

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
            throw new NotesV3Exception(
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

                    if (totalBytes > MaxPdfBytes)
                    {
                        throw new NotesV3Exception(
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

    private static string NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new NotesV3Exception(
                400, "Thiếu tên file PDF.");
        }

        // Xử lý cả hai kiểu dấu phân cách đường dẫn.
        var name = fileName
            .Replace('\\', '/')
            .Split('/')
            .Last()
            .Trim();

        if (string.IsNullOrWhiteSpace(name) ||
    name.Length <= 4 ||
    name.Length > 200 ||
            name.Any(char.IsControl) ||
            !name.EndsWith(
                ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotesV3Exception(
                400, "Tên file phải có đuôi .pdf và tối đa 200 ký tự.");
        }

        return name;
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

    private static NotesV3Exception MissingFile() =>
        new(404, "Không tìm thấy file PDF.");
}