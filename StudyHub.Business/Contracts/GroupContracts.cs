namespace StudyHub.Business.Contracts;

public sealed record GroupSummaryResponse(
    string Id,
    string Name,
    string Subject,
    string SubjectCssClass,
    string Description,
    int MemberCount,
    bool IsMember,
    bool IsOwner,
    bool IsPublic);

public sealed record GroupDetailsResponse(
    string Id,
    string Name,
    string Subject,
    string SubjectCssClass,
    string Description,
    int MemberCount,
    bool IsMember,
    bool IsOwner,
    IReadOnlyList<GroupChatMessageResponse> Messages);

public sealed record GroupChatMessageResponse(
    string Id,
    string AuthorName,
    string Content,
    DateTimeOffset SentAt,
    bool Mine);

public sealed record SendGroupMessageRequest(string? Content);

public sealed record CreateGroupRequest(
    string? Name,
    string? Subject,
    string? Description,
    bool IsPublic = true);
