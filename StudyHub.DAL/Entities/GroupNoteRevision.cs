namespace StudyHub.DAL.Entities;

public sealed class GroupNoteRevision
{
    public long GroupNoteRevisionId { get; set; }
    public long GroupNoteId { get; set; }
    public long StudyGroupId { get; set; }
    public long? ActorUserId { get; set; }
    public string Operation { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string ContentDeltaJson { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
