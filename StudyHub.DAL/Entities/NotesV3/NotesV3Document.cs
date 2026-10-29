namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3Document
{
    public long DocumentId { get; set; }
    public long OwnerUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}