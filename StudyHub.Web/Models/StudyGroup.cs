namespace StudyHub.Web.Models;

public sealed class StudyGroup
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Subject { get; set; }
    public required string SubjectCssClass { get; set; }
    public required string Description { get; set; }
    public bool IsPublic { get; set; }
    public required string OwnerUserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public HashSet<string> MemberUserIds { get; init; } = [];
    public List<GroupChatMessage> Messages { get; init; } = [];
}

public sealed class GroupChatMessage
{
    public required string Id { get; init; }
    public required string UserId { get; init; }
    public required string AuthorName { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}
