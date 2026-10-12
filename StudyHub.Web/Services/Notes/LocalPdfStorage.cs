using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace StudyHub.Web.Services.Notes;

public sealed class LocalPdfStorage(IWebHostEnvironment environment) : IPdfStorage
{
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "PdfFiles");

    public async Task<(string StorageKey, string Sha256)> SaveAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Root);
        var key = $"{Guid.NewGuid():N}.pdf";
        var path = GetPhysicalPath(key);
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var input = file.OpenReadStream())
                await input.CopyToAsync(output, cancellationToken);

            await using var saved = File.OpenRead(path);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(saved, cancellationToken));
            return (key, hash);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public string GetPhysicalPath(string storageKey)
    {
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(storageKey), "N", out _) ||
            !string.Equals(Path.GetExtension(storageKey), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Storage key PDF không hợp lệ.", nameof(storageKey));

        return Path.Combine(Root, storageKey);
    }

    public void Delete(string storageKey)
    {
        var path = GetPhysicalPath(storageKey);
        if (File.Exists(path)) File.Delete(path);
    }
}
