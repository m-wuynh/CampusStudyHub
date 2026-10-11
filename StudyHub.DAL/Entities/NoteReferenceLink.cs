namespace StudyHub.DAL.Entities;

public sealed class NoteReferenceLink
{
    public long NoteReferenceLinkId { get; set; }
    public long NoteId { get; set; }
    public long DocumentVersionId { get; set; }
    public int PageIndex { get; set; }
    public string CaptureType { get; set; } = null!;
    public string AnchorJson { get; set; } = null!;
    public string? CapturedText { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
