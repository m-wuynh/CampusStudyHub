namespace StudyHub.DAL.Entities;

public sealed class GroupStudySession
{
    public long Id { get; set; }
    public long StudyGroupId { get; set; }
    public long CreatedByUserId { get; set; }
    public string Title { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Preparation { get; set; } = "";
    public string Location { get; set; } = "";
    public string? MeetingUrl { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string Summary { get; set; } = "";
    public int Revision { get; set; } = 1;
    public ICollection<GroupSessionAttendance> Attendees { get; set; } = new List<GroupSessionAttendance>();
}

public sealed class GroupSessionAttendance
{
    public long SessionId { get; set; }
    public long UserId { get; set; }
    public bool Attended { get; set; }
    public GroupStudySession Session { get; set; } = null!;
    public User User { get; set; } = null!;
}

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

public sealed class GroupAssignmentAssignee
{
    public long AssignmentId { get; set; }
    public long UserId { get; set; }
    public GroupAssignment Assignment { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<GroupSubmission> Submissions { get; set; } = new List<GroupSubmission>();
}

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

public sealed class GroupQuestion
{
    public long Id { get; set; }
    public long StudyGroupId { get; set; }
    public long AuthorUserId { get; set; }
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long? AcceptedAnswerId { get; set; }
    public int Revision { get; set; } = 1;
    public User Author { get; set; } = null!;
    public ICollection<GroupAnswer> Answers { get; set; } = new List<GroupAnswer>();
}

public sealed class GroupAnswer
{
    public long Id { get; set; }
    public long QuestionId { get; set; }
    public long AuthorUserId { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public GroupQuestion Question { get; set; } = null!;
    public User Author { get; set; } = null!;
}

public sealed class GroupWeeklyGoal
{
    public long Id { get; set; }
    public long StudyGroupId { get; set; }
    public DateOnly WeekStart { get; set; }
    public string Title { get; set; } = "";
    public int TargetSessions { get; set; }
    public int TargetAssignments { get; set; }
    public int Revision { get; set; } = 1;
}
