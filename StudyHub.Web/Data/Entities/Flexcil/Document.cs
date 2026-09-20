namespace StudyHub.Web.Data.Entities.Flexcil;

public class Document
{
    public int Id { get; set; }

    // Document owner
    public string UserId { get; set; } = string.Empty;

    // Basic file information
    public string FileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/pdf";

    public long FileSize { get; set; }

    // PDF information
    public int PageCount { get; set; }

    // Optional thumbnail
    public string? ThumbnailPath { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
