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
