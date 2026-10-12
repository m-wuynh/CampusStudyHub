namespace StudyHub.DAL.Entities;

public sealed class DocumentVersion
{
    public long DocumentVersionId { get; set; }
    public long DocumentId { get; set; }
    public string StorageKey { get; set; } = null!;
    public string Sha256 { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string MimeType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }
    public int? PageCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
