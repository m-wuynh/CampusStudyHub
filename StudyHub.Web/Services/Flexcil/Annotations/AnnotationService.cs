using Microsoft.EntityFrameworkCore;
using StudyHub.Web.Data;
using StudyHub.Web.Data.Entities.Flexcil;

namespace StudyHub.Web.Services.Flexcil.Annotations;

public class AnnotationService : IAnnotationService
{
    private readonly ApplicationDbContext _context;

    public AnnotationService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Annotation>> GetByDocumentAsync(
        int documentId,
        string userId)
    {
        return await _context.Annotations
            .Where(x =>
                x.DocumentId == documentId &&
                x.UserId == userId)
            .OrderBy(x => x.PageNumber)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Annotation>> GetByPageAsync(
        int documentId,
        int pageNumber,
        string userId)
    {
        return await _context.Annotations
            .Where(x =>
                x.DocumentId == documentId &&
                x.PageNumber == pageNumber &&
                x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Annotation> CreateAsync(
        Annotation annotation)
    {
        annotation.CreatedAt = DateTime.UtcNow;
        annotation.UpdatedAt = DateTime.UtcNow;

        _context.Annotations.Add(annotation);

        await _context.SaveChangesAsync();

        return annotation;
    }

    public async Task DeleteAsync(
        int id,
        string userId)
    {
        var annotation =
            await _context.Annotations
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

        if (annotation == null)
        {
            return;
        }

        _context.Annotations.Remove(annotation);

        await _context.SaveChangesAsync();
    }
}
