using StudyHub.Web.Data.Entities.Flexcil;

namespace StudyHub.Web.Services.Flexcil.Annotations;

public interface IAnnotationService
{
    Task<List<Annotation>> GetByDocumentAsync(
        int documentId,
        string userId);

    Task<List<Annotation>> GetByPageAsync(
        int documentId,
        int pageNumber,
        string userId);

    Task<Annotation> CreateAsync(
        Annotation annotation);

    Task DeleteAsync(
        int id,
        string userId);
}
