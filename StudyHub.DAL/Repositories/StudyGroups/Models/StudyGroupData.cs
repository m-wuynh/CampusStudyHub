namespace StudyHub.DAL.Repositories.StudyGroups.Models;

public sealed class StudyGroupData
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
    public List<GroupChatMessageData> Messages { get; init; } = [];
}

public sealed class GroupChatMessageData
{
    public required string Id { get; init; }
    public required string UserId { get; init; }
    public required string AuthorName { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}
