namespace StudyHub.DAL.Entities;

public sealed class GroupAssignment
{
    public long Id { get; set; }
    public long StudyGroupId { get; set; }
    public long CreatedByUserId { get; set; }
    public long? SessionId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset DueAtUtc { get; set; }
    public bool AllowLateSubmission { get; set; }
    public string Status { get; set; } = "Open";
    public int Revision { get; set; } = 1;
    public ICollection<GroupAssignmentAssignee> Assignees { get; set; } = new List<GroupAssignmentAssignee>();
}
