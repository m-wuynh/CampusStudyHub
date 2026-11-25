namespace StudyHub.DAL.Entities;

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
