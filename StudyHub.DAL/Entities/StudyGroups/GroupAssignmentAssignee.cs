namespace StudyHub.DAL.Entities;

public sealed class GroupAssignmentAssignee
{
    public long AssignmentId { get; set; }
    public long UserId { get; set; }
    public GroupAssignment Assignment { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<GroupSubmission> Submissions { get; set; } = new List<GroupSubmission>();
}
