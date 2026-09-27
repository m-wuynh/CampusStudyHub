namespace StudyHub.DAL.Repositories.StudyGroups.Models;

public sealed class StudyGroupData
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Subject { get; set; }
    public required string SubjectCssClass { get; set; }
    public required string Description { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string MeetingFormat { get; set; } = "Online";
    public string MeetingSchedule { get; set; } = string.Empty;
    public string ContactUrl { get; set; } = string.Empty;
    public string Rules { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public string JoinMode { get; set; } = "Approval";
    public short MaxMembers { get; set; } = 20;
    public required string OwnerUserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? CurrentMembershipStatus { get; set; }
    public string? CurrentMemberRole { get; set; }
    public List<GroupMemberData> Members { get; init; } = [];
    public List<GroupPostData> Posts { get; init; } = [];
    public List<GroupResourceData> Resources { get; init; } = [];
}

public sealed class GroupMemberData
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required string Role { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset? JoinedAt { get; set; }
}

public sealed class GroupPostData
{
    public required string Id { get; init; }
    public required string UserId { get; init; }
    public required string AuthorName { get; init; }
    public string? AuthorAvatarUrl { get; init; }
    public required string Content { get; init; }
    public required string PostType { get; init; }
    public bool IsPinned { get; init; }
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record GroupResourceData(string Id, string Title, string ResourceType, string? Url);

public sealed record GroupMembershipData(string GroupId, string Status, string Message);

public sealed record GroupInviteData(
    string InviteCode,
    string GroupId,
    string GroupName,
    string Subject,
    DateTimeOffset ExpiresAt,
    int MaxUses,
    int UseCount);

public sealed class CreateStudyGroupData
{
    public required long OwnerUserId { get; init; }
    public required string Name { get; init; }
    public required string Subject { get; init; }
    public string? Description { get; init; }
    public string? Goal { get; init; }
    public required string MeetingFormat { get; init; }
    public string? MeetingSchedule { get; init; }
    public string? ContactUrl { get; init; }
    public string? Rules { get; init; }
    public required string Visibility { get; init; }
    public required string JoinMode { get; init; }
    public short MaxMembers { get; init; }
}

public sealed class StudyGroupRepositoryException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
