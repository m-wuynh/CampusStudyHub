namespace StudyHub.DAL.Entities;

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
