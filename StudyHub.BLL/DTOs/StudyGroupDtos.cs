namespace StudyHub.BLL.DTOs;

public sealed record GroupSummaryResponse(
    string Id,
    string Name,
    string Subject,
    string SubjectCssClass,
    IReadOnlyList<GroupSubjectResponse> Subjects,
    string Description,
    string Goal,
    string MeetingFormat,
    string MeetingSchedule,
    int MemberCount,
    int? MaxMembers,
    int PendingMemberCount,
    string? MembershipStatus,
    string? MemberRole,
    bool IsMember,
    bool IsOwner,
    bool IsPublic,
    string JoinMode);

public sealed record GroupDetailsResponse(
    string Id,
    string Name,
    string Subject,
    string SubjectCssClass,
    IReadOnlyList<GroupSubjectResponse> Subjects,
    string Description,
    string Goal,
    string MeetingFormat,
    string MeetingSchedule,
    string Rules,
    int MemberCount,
    int? MaxMembers,
    string? MembershipStatus,
    string? MemberRole,
    bool IsMember,
    bool IsOwner,
    bool CanManage,
    bool IsPublic,
    string JoinMode,
    IReadOnlyList<GroupMemberResponse> Members,
    IReadOnlyList<GroupPostResponse> Announcements,
    IReadOnlyList<GroupPostResponse> Messages,
    IReadOnlyList<GroupResourceResponse> Resources);

public sealed record GroupMemberResponse(
    string UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? JoinedAt,
    bool IsCurrentUser);

public sealed record GroupPostResponse(
    string Id,
    string AuthorUserId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string Content,
    string PostType,
    bool IsPinned,
    DateTimeOffset SentAt,
    bool Mine);

public sealed record GroupResourceResponse(string Id, string Title, string ResourceType, string? Url);

public sealed record GroupSubjectResponse(string Name, string CssClass, bool IsCustom);

public sealed record GroupSubjectOptionResponse(string Name, bool IsCustom);

public sealed record GroupMembershipResponse(string GroupId, string Status, string Message);

public sealed record GroupInviteResponse(
    string InviteCode,
    string GroupId,
    string GroupName,
    string Subject,
    DateTimeOffset ExpiresAt,
    int MaxUses,
    int UseCount);

public sealed record SendGroupMessageRequest(string? Content);
public sealed record CreateAnnouncementRequest(string? Content, bool IsPinned = false);
public sealed record ManageGroupMemberRequest(string? Action);
public sealed record CreateGroupInviteRequest(int ExpiresInDays = 7, int MaxUses = 20);

public sealed record CreateGroupRequest(
    string? Name,
    string? Subject,
    IReadOnlyList<string>? Subjects,
    string? Description,
    string? Goal,
    string? MeetingFormat,
    string? MeetingSchedule,
    string? Rules,
    bool IsPublic = true,
    string? JoinMode = "Approval",
    short? MaxMembers = null);

public sealed record UpdateGroupRequest(
    string? Name,
    string? Subject,
    IReadOnlyList<string>? Subjects,
    string? Description,
    string? Goal,
    string? MeetingFormat,
    string? MeetingSchedule,
    string? Rules,
    bool IsPublic = true,
    string? JoinMode = "Approval",
    short? MaxMembers = null);
