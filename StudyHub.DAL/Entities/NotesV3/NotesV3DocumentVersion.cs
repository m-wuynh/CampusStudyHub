namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3DocumentVersion
{
    public long DocumentVersionId { get; set; }
    public long DocumentId { get; set; }

    public string StorageKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    // NULL cho tới khi xác định được số trang.
    public int? PageCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}