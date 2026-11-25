namespace StudyHub.DAL.Entities;

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
