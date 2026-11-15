namespace StudyHub.DAL.Entities;

// Each resubmission creates a new revision; previous work and feedback are retained.
public sealed class GroupSubmission
{
    public long Id { get; set; }
    public long AssignmentId { get; set; }
    public long UserId { get; set; }
    public int Version { get; set; }
    public string Content { get; set; } = "";
    public string? ResourceUrl { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public string Status { get; set; } = "Submitted";
    public string Feedback { get; set; } = "";
    public long? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public GroupAssignmentAssignee Assignee { get; set; } = null!;
}
