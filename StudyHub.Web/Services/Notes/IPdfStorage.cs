using Microsoft.AspNetCore.Http;

namespace StudyHub.Web.Services.Notes;

public interface IPdfStorage
{
    Task<(string StorageKey, string Sha256)> SaveAsync(IFormFile file, CancellationToken cancellationToken);
    string GetPhysicalPath(string storageKey);
    void Delete(string storageKey);
}
