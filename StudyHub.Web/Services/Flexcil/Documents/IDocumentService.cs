using Microsoft.AspNetCore.Http;
using StudyHub.Web.Data.Entities.Flexcil;

namespace StudyHub.Web.Services.Flexcil.Documents;

public interface IDocumentService
{
    Task<Document> UploadAsync(
        IFormFile file,
        string userId);

    Task<List<Document>> GetByUserAsync(
        string userId);

    Task<Document?> GetByIdAsync(
        int id,
        string userId);

    Task DeleteAsync(
        int id,
        string userId);
}
