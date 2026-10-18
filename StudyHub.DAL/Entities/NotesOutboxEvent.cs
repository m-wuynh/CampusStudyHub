namespace StudyHub.DAL.Entities;

public sealed class NotesOutboxEvent
{
    public long NotesOutboxEventId { get; set; }
    public long StudyGroupId { get; set; }
    public string EventName { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public int AttemptCount { get; set; }
}
