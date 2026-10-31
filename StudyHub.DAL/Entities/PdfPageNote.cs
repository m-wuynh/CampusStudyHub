namespace StudyHub.DAL.Entities;

/// <summary>Places one private Quill note on a page in its owner's PDF notebook.</summary>
public sealed class PdfPageNote
{
    public long NoteId { get; set; }
    public long OwnerUserId { get; set; }
    public long DocumentId { get; set; }
    public int PageIndex { get; set; }
}
