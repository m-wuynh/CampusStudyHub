namespace StudyHub.DAL.Entities.Pdf;

public sealed class PdfDocument
{
    public long DocumentId { get; set; }
    public long OwnerUserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? OriginalFileNameKey { get; set; }
    public int PageCount { get; set; }
    public long FileSizeBytes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
