namespace StudyHub.DAL.Entities.Pdf;

public sealed class PdfDocumentPageNote
{
    public long PdfDocumentPageNoteId { get; set; }
    public long DocumentId { get; set; }
    public long OwnerUserId { get; set; }
    public int PhysicalPageNumber { get; set; }
    public string ContentText { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}
