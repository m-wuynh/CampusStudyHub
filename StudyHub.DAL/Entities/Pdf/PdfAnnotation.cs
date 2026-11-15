namespace StudyHub.DAL.Entities.Pdf;

public sealed class PdfAnnotation
{
    public long AnnotationId { get; set; }
    public long DocumentId { get; set; }
    public long OwnerUserId { get; set; }

    public int PhysicalPageNumber { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = [];
}
