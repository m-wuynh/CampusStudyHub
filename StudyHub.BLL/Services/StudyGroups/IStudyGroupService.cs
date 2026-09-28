using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.StudyGroups;

public interface IStudyGroupService
{
    Task<IReadOnlyList<GroupSummaryResponse>> GetGroupsAsync(string? searchText = null, CancellationToken cancellationToken = default);
    Task<GroupDetailsResponse?> GetGroupAsync(string groupId, CancellationToken cancellationToken = default);
    Task<GroupMembershipResponse> JoinAsync(string groupId, CancellationToken cancellationToken = default);
    Task<GroupMembershipResponse> LeaveOrWithdrawAsync(string groupId, CancellationToken cancellationToken = default);
    Task<GroupPostResponse> AddMessageAsync(string groupId, string content, CancellationToken cancellationToken = default);
    Task<GroupPostResponse> AddAnnouncementAsync(string groupId, string content, bool isPinned, CancellationToken cancellationToken = default);
    Task<GroupDetailsResponse> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken = default);
    Task ManageMemberAsync(string groupId, string targetUserId, string action, CancellationToken cancellationToken = default);
    Task<GroupInviteResponse> CreateInviteAsync(string groupId, int expiresInDays, int maxUses, CancellationToken cancellationToken = default);
    Task<GroupInviteResponse?> GetInviteAsync(string inviteCode, CancellationToken cancellationToken = default);
    Task<GroupMembershipResponse> AcceptInviteAsync(string inviteCode, CancellationToken cancellationToken = default);
    Task<bool> CanAccessChatAsync(string groupId, CancellationToken cancellationToken = default);
}

public sealed class StudyGroupException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
