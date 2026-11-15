namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3Annotation
{
    public long AnnotationId { get; set; }
    public long DocumentVersionId { get; set; }

    public long? OwnerUserId { get; set; }
    public long? GroupDocumentShareId { get; set; }

    public int PhysicalPageNumber { get; set; }

    // Chỉ chứa highlight hoặc text comment đã được BLL kiểm tra.
    public string PayloadJson { get; set; } = "{}";

    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = [];
}