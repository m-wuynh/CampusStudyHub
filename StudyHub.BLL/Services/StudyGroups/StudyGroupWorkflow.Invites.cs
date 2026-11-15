using StudyHub.DAL.Entities.Pdf;
using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using StudyHub.BLL.Services.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed partial class StudyGroupWorkflow
{
    public async Task<GroupInviteData> CreateInviteAsync(
        long groupId,
        long actorUserId,
        int expiresInDays,
        int maxUses,
        CancellationToken cancellationToken = default)
    {
        var group = await data.Query<StudyGroup>(false)
            .Include(item => item.StudyGroupSubjects)
                .ThenInclude(subject => subject.Subject)
            .Include(item => item.GroupMembers)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupWorkflowException("not_found", "Không tìm thấy nhóm học tập.");
        var actor = group.GroupMembers.SingleOrDefault(item => item.UserId == actorUserId);
        if (actor?.Status != "Active" || (group.OwnerUserId != actorUserId && actor.MemberRole != "Moderator"))
            throw new StudyGroupWorkflowException("forbidden", "Bạn không có quyền tạo lời mời.");

        var invite = new GroupInvite
        {
            InviteCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
            StudyGroupId = groupId,
            CreatedByUserId = actorUserId,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(expiresInDays),
            MaxUses = maxUses,
            UseCount = 0,
            IsRevoked = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        await data.AddAsync(invite);
        await data.SaveChangesAsync(cancellationToken);
        return ToInvite(invite, group);
    }

    public async Task<GroupInviteData?> GetInviteAsync(
        string inviteCode,
        CancellationToken cancellationToken = default)
    {
        var invite = await data.Query<GroupInvite>(false).AsNoTracking()
            .Include(item => item.StudyGroup)
                .ThenInclude(group => group.StudyGroupSubjects)
                    .ThenInclude(subject => subject.Subject)
            .SingleOrDefaultAsync(item => item.InviteCode == inviteCode, cancellationToken);
        if (invite is null || !InviteIsValid(invite) || invite.StudyGroup.IsArchived) return null;
        return ToInvite(invite, invite.StudyGroup);
    }

    public async Task<GroupMembershipData> AcceptInviteAsync(
        string inviteCode,
        long userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await data.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var invite = await data.Query<GroupInvite>(false)
            .Include(item => item.StudyGroup)
                .ThenInclude(group => group.GroupMembers)
            .SingleOrDefaultAsync(item => item.InviteCode == inviteCode, cancellationToken)
            ?? throw new StudyGroupWorkflowException("invite_not_found", "Lời mời không tồn tại.");
        if (!InviteIsValid(invite) || invite.StudyGroup.IsArchived)
            throw new StudyGroupWorkflowException("invite_expired", "Lời mời đã hết hạn hoặc hết lượt sử dụng.");

        var group = invite.StudyGroup;
        var membership = group.GroupMembers.SingleOrDefault(item => item.UserId == userId);
        if (membership?.Status == "Banned")
            throw new StudyGroupWorkflowException("banned", "Bạn đã bị cấm tham gia nhóm này.");
        if (membership?.Status == "Active")
            return new GroupMembershipData(group.StudyGroupId.ToString(), "Active", "Bạn đã là thành viên của nhóm.");

        EnsureCapacity(group);
        var now = DateTime.UtcNow;
        if (membership is null)
        {
            await data.AddAsync(new GroupMember
            {
                StudyGroupId = group.StudyGroupId,
                UserId = userId,
                MemberRole = "Member",
                Status = "Active",
                RequestedAtUtc = now,
                JoinedAtUtc = now
            });
        }
        else
        {
            membership.MemberRole = "Member";
            membership.Status = "Active";
            membership.RequestedAtUtc = now;
            membership.JoinedAtUtc = now;
        }

        invite.UseCount++;
        await data.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GroupMembershipData(group.StudyGroupId.ToString(), "Active", "Đã tham gia nhóm bằng lời mời.");
    }

}
