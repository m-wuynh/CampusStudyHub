using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.DAL.Repositories.StudyGroups;

public interface IStudyGroupRepository
{
    Task<IReadOnlyList<StudyGroupData>> GetVisibleAsync(
        long currentUserId,
        string? searchText = null,
        CancellationToken cancellationToken = default);

    Task<StudyGroupData?> GetAsync(
        long groupId,
        long currentUserId,
        CancellationToken cancellationToken = default);

    Task<StudyGroupData> CreateAsync(
        CreateStudyGroupData group,
        CancellationToken cancellationToken = default);

    Task<GroupMembershipData> RequestJoinAsync(
        long groupId,
        long userId,
        CancellationToken cancellationToken = default);

    Task<GroupMembershipData> LeaveOrWithdrawAsync(
        long groupId,
        long userId,
        CancellationToken cancellationToken = default);

    Task<GroupPostData> AddPostAsync(
        long groupId,
        long userId,
        string content,
        string postType,
        bool isPinned,
        CancellationToken cancellationToken = default);

    Task ManageMemberAsync(
        long groupId,
        long actorUserId,
        long targetUserId,
        string action,
        CancellationToken cancellationToken = default);

    Task<GroupInviteData> CreateInviteAsync(
        long groupId,
        long actorUserId,
        int expiresInDays,
        int maxUses,
        CancellationToken cancellationToken = default);

    Task<GroupInviteData?> GetInviteAsync(
        string inviteCode,
        CancellationToken cancellationToken = default);

    Task<GroupMembershipData> AcceptInviteAsync(
        string inviteCode,
        long userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsActiveMemberAsync(
        long groupId,
        long userId,
        CancellationToken cancellationToken = default);
}
