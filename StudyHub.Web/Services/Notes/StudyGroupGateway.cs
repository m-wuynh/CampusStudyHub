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
        return await db.GroupMembers.AsNoTracking().AnyAsync(member =>
            member.UserId == userId && member.StudyGroupId == groupId && member.Status == "Active" &&
            !member.StudyGroup.IsArchived && member.User.Status == "Active" &&
            member.StudyGroup.OwnerUser.Status == "Active", cancellationToken);
    }

    public async Task<IReadOnlyList<NoteGroupOption>> GetActiveGroupsAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (userId != currentUser.UserId) return [];
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.UserId == userId && user.Status == "Active", cancellationToken))
            return [];
        return await db.GroupMembers.AsNoTracking()
            .Where(member => member.UserId == userId && member.Status == "Active" &&
                !member.StudyGroup.IsArchived && member.StudyGroup.OwnerUser.Status == "Active")
            .OrderBy(member => member.StudyGroup.GroupName)
            .Select(member => new NoteGroupOption(member.StudyGroupId, member.StudyGroup.GroupName))
            .ToListAsync(cancellationToken);
    }

    public async Task<NoteGroupDescriptor?> GetActiveGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        if (groupId <= 0) return null;
        return await db.StudyGroups.AsNoTracking()
            .Where(group => group.StudyGroupId == groupId && !group.IsArchived &&
                group.OwnerUser.Status == "Active")
            .Select(group => new NoteGroupDescriptor(group.StudyGroupId, group.OwnerUserId, group.GroupName))
            .FirstOrDefaultAsync(cancellationToken);
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
