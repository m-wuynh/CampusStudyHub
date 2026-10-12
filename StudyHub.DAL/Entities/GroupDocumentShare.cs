namespace StudyHub.DAL.Entities;

public sealed class GroupDocumentShare
{
    public long GroupDocumentShareId { get; set; }
    public long DocumentId { get; set; }
    public long StudyGroupId { get; set; }
    public long SharedByUserId { get; set; }
    public bool IsActive { get; set; }
    public DateTime SharedAtUtc { get; set; }
    public DateTime? UnsharedAtUtc { get; set; }
}
