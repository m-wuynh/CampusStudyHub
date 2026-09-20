using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StudyHub.Web.Data;
using StudyHub.Web.Data.Entities.Flexcil;

namespace StudyHub.Web.Services.Flexcil.Documents;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public DocumentService(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<Document> UploadAsync(
        IFormFile file,
        string userId)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty.");

        if (!file.FileName.EndsWith(
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Only PDF files are allowed.");
        }

        const long maxFileSize = 50 * 1024 * 1024;

        if (file.Length > maxFileSize)
        {
            throw new ArgumentException(
                "File size cannot exceed 50 MB.");
        }

        var uploadDirectory = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "documents");

        Directory.CreateDirectory(uploadDirectory);

        var storedFileName =
            $"{Guid.NewGuid():N}.pdf";

        var filePath = Path.Combine(
            uploadDirectory,
            storedFileName);

        await using (var stream = new FileStream(
            filePath,
            FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var document = new Document
        {
            UserId = userId,
            FileName = Path.GetFileName(file.FileName),
            StoredFileName = storedFileName,
            FilePath = $"/uploads/documents/{storedFileName}",
            ContentType = "application/pdf",
            FileSize = file.Length,
            PageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);

        await _context.SaveChangesAsync();

        return document;
    }

    public async Task<List<Document>> GetByUserAsync(
        string userId)
    {
        return await _context.Documents
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Document?> GetByIdAsync(
        int id,
        string userId)
    {
        return await _context.Documents
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.UserId == userId);
    }

    public async Task DeleteAsync(
        int id,
        string userId)
    {
        var document = await GetByIdAsync(id, userId);

        if (document == null)
            return;

        var physicalPath = Path.Combine(
            _environment.WebRootPath,
            document.FilePath.TrimStart('/')
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar));

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        _context.Documents.Remove(document);

        await _context.SaveChangesAsync();
    }
}
