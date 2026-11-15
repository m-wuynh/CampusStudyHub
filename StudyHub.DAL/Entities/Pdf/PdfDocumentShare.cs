namespace StudyHub.DAL.Entities.Pdf;

public sealed class PdfDocumentShare
{
    public long PdfDocumentShareId { get; set; }

    public long DocumentId { get; set; }
    public long StudyGroupId { get; set; }
    public long SharedByUserId { get; set; }

    public bool IsActive { get; set; }

    public DateTime SharedAtUtc { get; set; }
    public DateTime? UnsharedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
