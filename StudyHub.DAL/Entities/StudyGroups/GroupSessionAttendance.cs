namespace StudyHub.DAL.Entities;

public sealed class GroupSessionAttendance
{
    public long SessionId { get; set; }
    public long UserId { get; set; }
    public bool Attended { get; set; }
    public GroupStudySession Session { get; set; } = null!;
    public User User { get; set; } = null!;
}
