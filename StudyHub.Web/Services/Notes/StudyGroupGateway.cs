using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Notes;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.DAL.Persistence;

namespace StudyHub.Web.Services.Notes;

/// <summary>Notes boundary for group access; its Development fixture never creates users.</summary>
public sealed class StudyGroupGateway(
    IStudyGroupService studyGroups,
    ICurrentUser currentUser,
    StudyHubDbContext db) : IStudyGroupGateway
{
    private const string DevelopmentGroupName = "[DEV] Notes Role Smoke";

    public async Task<bool> GroupExistsAndIsActiveAsync(long groupId, CancellationToken cancellationToken = default) =>
        await GetActiveGroupAsync(groupId, cancellationToken) is not null;

    public async Task<bool> IsActiveMemberAsync(long userId, long groupId, CancellationToken cancellationToken = default)
    {
        if (userId != currentUser.UserId) return false;
        var group = await studyGroups.GetGroupAsync(groupId.ToString(), cancellationToken);
        return group is { IsMember: true };
    }

    public async Task<IReadOnlyList<NoteGroupOption>> GetActiveGroupsAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (userId != currentUser.UserId) return [];
        var groups = await studyGroups.GetGroupsAsync(cancellationToken: cancellationToken);
        return groups.Where(group => group.IsMember && long.TryParse(group.Id, out _))
            .Select(group => new NoteGroupOption(long.Parse(group.Id), group.Name))
            .ToList();
    }

    public async Task<NoteGroupDescriptor?> GetActiveGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        if (groupId <= 0) return null;
        var group = await studyGroups.GetGroupAsync(groupId.ToString(), cancellationToken);
        return group is null ? null : new NoteGroupDescriptor(groupId, group.OwnerUserId, group.Name);
    }

    public async Task<NoteGroupDescriptor?> GetDevelopmentTestGroupAsync(
        long ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (ownerUserId != currentUser.UserId) return null;
        return await db.StudyGroups.AsNoTracking()
            .Where(group => group.OwnerUserId == ownerUserId && !group.IsArchived && group.GroupName == DevelopmentGroupName)
            .OrderByDescending(group => group.StudyGroupId)
            .Select(group => new NoteGroupDescriptor(group.StudyGroupId, group.OwnerUserId, group.GroupName))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NoteGroupDescriptor> CreateDevelopmentTestGroupAsync(
        long ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (ownerUserId != currentUser.UserId)
            throw new UnauthorizedAccessException("Chỉ có thể tạo nhóm kiểm thử cho tài khoản hiện tại.");

        var existing = await GetDevelopmentTestGroupAsync(ownerUserId, cancellationToken);
        if (existing is not null) return existing;

        var created = await studyGroups.CreateGroupAsync(new CreateGroupRequest(
            Name: DevelopmentGroupName,
            Subject: "Tự học",
            Subjects: null,
            Description: "Nhóm phát triển tạm dùng để kiểm tra quyền Notes khi database chỉ có một tài khoản.",
            Goal: "Smoke test Owner và Member giả lập trong module Notes.",
            MeetingFormat: "Online",
            MeetingSchedule: null,
            Rules: "Chỉ dùng trong môi trường Development.",
            IsPublic: false,
            JoinMode: "InviteOnly",
            MaxMembers: 10), cancellationToken);

        return new NoteGroupDescriptor(long.Parse(created.Id), created.OwnerUserId, created.Name);
    }

    public async Task<IReadOnlyList<NotesDevelopmentPersona>> GetDevelopmentPersonasAsync(
        CancellationToken cancellationToken = default)
    {
        // Read-only Development helper: discover real owner/member identities without creating seed users.
        var pair = await db.GroupMembers.AsNoTracking()
            .Where(member => !member.StudyGroup.IsArchived &&
                member.StudyGroup.OwnerUser.Status == "Active" &&
                member.Status == "Active" &&
                member.User.Status == "Active" &&
                member.UserId != member.StudyGroup.OwnerUserId)
            .OrderBy(member => member.StudyGroupId)
            .ThenBy(member => member.UserId)
            .Select(member => new
            {
                GroupId = member.StudyGroupId,
                GroupName = member.StudyGroup.GroupName,
                OwnerId = member.StudyGroup.OwnerUser.UserId,
                OwnerName = member.StudyGroup.OwnerUser.DisplayName,
                OwnerEmail = member.StudyGroup.OwnerUser.Email,
                OwnerRole = member.StudyGroup.OwnerUser.RoleCode,
                MemberId = member.User.UserId,
                MemberName = member.User.DisplayName,
                MemberEmail = member.User.Email,
                MemberRole = member.User.RoleCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (pair is null) return Array.Empty<NotesDevelopmentPersona>();

        return new[]
        {
            new NotesDevelopmentPersona("real-owner", pair.OwnerId, pair.OwnerName, pair.OwnerEmail,
                pair.OwnerRole, "Chủ nhóm · tài khoản thật", pair.GroupId, pair.GroupName),
            new NotesDevelopmentPersona("real-member", pair.MemberId, pair.MemberName, pair.MemberEmail,
                pair.MemberRole, "Thành viên Active · tài khoản thật", pair.GroupId, pair.GroupName)
        };
    }
}
