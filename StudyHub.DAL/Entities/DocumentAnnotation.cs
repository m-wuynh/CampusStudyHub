namespace StudyHub.DAL.Entities;

public sealed class DocumentAnnotation
{
    public long DocumentVersionId { get; set; }
    public long OwnerUserId { get; set; }
    public string AnnotationKey { get; set; } = null!;
    public int PageIndex { get; set; }
    public string PayloadJson { get; set; } = null!;
    public DateTime UpdatedAtUtc { get; set; }
}
