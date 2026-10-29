namespace StudyHub.DAL.Entities.NotesV3;

public sealed class NotesV3Section
{
    public long SectionId { get; set; }
    public long NotebookId { get; set; }

    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
}