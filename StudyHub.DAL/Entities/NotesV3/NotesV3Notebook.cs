namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3Notebook
{
    public long NotebookId { get; set; }

    // Với notebook nhóm, đây là người tạo, không phải quyền sở hữu độc quyền.
    public long OwnerUserId { get; set; }
    public long? StudyGroupId { get; set; }

    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}