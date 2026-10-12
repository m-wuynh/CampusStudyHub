namespace StudyHub.DAL.Entities;

public sealed class GroupNote
{
    public long GroupNoteId { get; set; }
    public long StudyGroupId { get; set; }
    public long OwnerUserId { get; set; }
    public long? SourceNoteId { get; set; }
    public string Title { get; set; } = null!;
    public string ContentDeltaJson { get; set; } = null!;
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
