namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3CollabUpdate
{
    public long UpdateId { get; set; }
    public long PageId { get; set; }
    public Guid OperationId { get; set; }
    public long ActorUserId { get; set; }

    public byte[] UpdateBytes { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
}