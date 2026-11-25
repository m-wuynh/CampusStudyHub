namespace StudyHub.DAL.Entities.Pdf;

public sealed class PdfDocumentPageNote
{
    public const int MaxContentLength = 140_000;

    public long PdfDocumentPageNoteId { get; set; }

    public long DocumentId { get; set; }

    public long OwnerUserId { get; set; }

    public int PhysicalPageNumber { get; set; }

    public string ContentText { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
