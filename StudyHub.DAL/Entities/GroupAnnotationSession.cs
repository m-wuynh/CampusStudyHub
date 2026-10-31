namespace StudyHub.DAL.Entities;

public sealed class GroupAnnotationSession
{
    public long GroupDocumentShareId { get; set; }
    public long DocumentVersionId { get; set; }
    public string PayloadJson { get; set; } = "[]";
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
