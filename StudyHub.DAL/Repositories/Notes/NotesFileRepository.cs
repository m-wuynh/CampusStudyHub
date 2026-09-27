using System.Text.Json;

namespace StudyHub.DAL.Repositories.Notes;

public sealed class NotesFileRepository(string root)
    : INotesFileRepository
{
    private string Folder(string key)
    {
        if (!Guid.TryParseExact(key, "N", out _))
        {
            throw new InvalidOperationException(
                "StorageKey không thuộc module Notes.");
        }

        return Path.Combine(root, key);
    }

    public async Task<string> AddPdfAsync(
        Stream input,
        CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N");

        Directory.CreateDirectory(Folder(key));

        try
        {
            await using var output = new FileStream(
                Path.Combine(Folder(key), "document.pdf"),
                FileMode.CreateNew,
                FileAccess.Write);

            await input.CopyToAsync(output, ct);

            return key;
        }
        catch
        {
            DeletePdf(key);
            throw;
        }
    }

    public Stream OpenPdf(string key)
    {
        return File.OpenRead(
            Path.Combine(Folder(key), "document.pdf"));
    }

    public void DeletePdf(string key)
    {
        var folder = Folder(key);

        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private async Task<FileStream> LockAsync(
        string folder,
        int page,
        CancellationToken ct)
    {
        Directory.CreateDirectory(folder);

        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    Path.Combine(folder, $"{page}.lock"),
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException) when (attempt < 80)
            {
                await Task.Delay(25, ct);
            }
        }
    }

    private static async Task<StoredNotePage?> ReadAsync(
        string path,
        CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, ct);

        return JsonSerializer.Deserialize<StoredNotePage>(json)
            ?? throw new InvalidDataException(
                "File ghi chú bị hỏng.");
    }

    public async Task<StoredNotePage?> ReadPageAsync(
        string key,
        int page,
        CancellationToken ct)
    {
        if (page is < 1 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        var folder = Folder(key);

        await using var guard =
            await LockAsync(folder, page, ct);

        return await ReadAsync(
            Path.Combine(folder, $"{page}.json"),
            ct);
    }

    public async Task<Guid?> SavePageAsync(
        string key,
        int page,
        Guid? expectedVersion,
        string payload,
        CancellationToken ct)
    {
        if (page is < 1 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        var folder = Folder(key);

        await using var guard =
            await LockAsync(folder, page, ct);

        var path = Path.Combine(folder, $"{page}.json");
        var existing = await ReadAsync(path, ct);

        if (existing?.Version != expectedVersion)
        {
            return null;
        }

        var version = Guid.NewGuid();

        var temporaryPath = Path.Combine(
            folder,
            $"{Guid.NewGuid():N}.tmp");

        try
        {
            var data = new StoredNotePage(version, payload);

            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(data),
                ct);

            File.Move(
                temporaryPath,
                path,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return version;
    }
}
