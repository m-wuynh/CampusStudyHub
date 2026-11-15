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
    public async Task<GroupMembershipData> RequestJoinAsync(
        long groupId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await data.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var group = await data.Query<StudyGroup>(false)
            .Include(item => item.GroupMembers)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupWorkflowException("not_found", "Không tìm thấy nhóm học tập.");

        if (group.Visibility != "Public" || group.JoinMode == "InviteOnly")
            throw new StudyGroupWorkflowException("invite_required", "Nhóm này chỉ cho phép tham gia bằng lời mời.");

        var membership = group.GroupMembers.SingleOrDefault(item => item.UserId == userId);
        if (membership?.Status == "Banned")
            throw new StudyGroupWorkflowException("banned", "Bạn đã bị cấm tham gia nhóm này.");
        if (membership?.Status == "Active")
            return new GroupMembershipData(groupId.ToString(), "Active", "Bạn đã là thành viên của nhóm.");
        if (membership?.Status == "Pending")
            return new GroupMembershipData(groupId.ToString(), "Pending", "Yêu cầu tham gia đang chờ duyệt.");

        EnsureCapacity(group);
        var now = DateTime.UtcNow;
        var status = group.JoinMode == "Open" ? "Active" : "Pending";
        if (membership is null)
        {
            membership = new GroupMember
            {
                StudyGroupId = groupId,
                UserId = userId,
                MemberRole = "Member",
                Status = status,
                RequestedAtUtc = now,
                JoinedAtUtc = status == "Active" ? now : null
            };
            await data.AddAsync(membership);
        }
        else
        {
            membership.MemberRole = "Member";
            membership.Status = status;
            membership.RequestedAtUtc = now;
            membership.JoinedAtUtc = status == "Active" ? now : null;
        }

        await data.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var message = status == "Active"
            ? "Bạn đã tham gia nhóm thành công."
            : "Đã gửi yêu cầu tham gia. Hãy chờ trưởng nhóm duyệt.";
        return new GroupMembershipData(groupId.ToString(), status, message);
    }

    public async Task<GroupMembershipData> LeaveOrWithdrawAsync(
        long groupId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await data.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var group = await data.Query<StudyGroup>(false)
            .Include(item => item.GroupMembers)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupWorkflowException("not_found", "Không tìm thấy nhóm học tập.");
        if (group.OwnerUserId == userId)
            throw new StudyGroupWorkflowException("owner_cannot_leave", "Trưởng nhóm phải chuyển quyền hoặc lưu trữ nhóm trước khi rời.");

        var membership = group.GroupMembers.SingleOrDefault(item => item.UserId == userId);
        if (membership is null || membership.Status is "Left" or "Rejected")
            return new GroupMembershipData(groupId.ToString(), "None", "Bạn hiện không tham gia nhóm.");
        if (membership.Status == "Banned")
            throw new StudyGroupWorkflowException("banned", "Bạn đã bị cấm khỏi nhóm này.");

        var wasPending = membership.Status == "Pending";
        membership.Status = "Left";
        membership.JoinedAtUtc = null;
        await RestorePrivateResourcesAsync(groupId, userId, cancellationToken);
        await data.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GroupMembershipData(
            groupId.ToString(),
            "Left",
            wasPending ? "Đã rút yêu cầu tham gia." : "Bạn đã rời nhóm.");
    }

    public async Task<GroupMembershipData> RequestUnbanAsync(long groupId, long userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await data.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var membership = await data.Query<GroupMember>(false)
            .Include(item => item.StudyGroup)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && item.UserId == userId, cancellationToken);
        if (membership is null || membership.StudyGroup.IsArchived)
            throw new StudyGroupWorkflowException("not_found", "Không tìm thấy nhóm học tập.");
        if (membership.Status != "Banned")
            throw new StudyGroupWorkflowException("invalid_action", "Bạn không bị cấm khỏi nhóm này.");
        if (membership.UnbanRequestedAtUtc is not null)
            return new GroupMembershipData(groupId.ToString(), "Banned", "Yêu cầu gỡ cấm đang chờ xử lý.");

        membership.UnbanRequestedAtUtc = DateTime.UtcNow;
        await data.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GroupMembershipData(groupId.ToString(), "Banned", "Đã gửi yêu cầu gỡ cấm tới người đã cấm bạn.");
    }

    public async Task ManageMemberAsync(
        long groupId,
        long actorUserId,
        long targetUserId,
        string action,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await data.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var group = await data.Query<StudyGroup>(false)
            .Include(item => item.GroupMembers)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupWorkflowException("not_found", "Không tìm thấy nhóm học tập.");
        var actor = group.GroupMembers.SingleOrDefault(item => item.UserId == actorUserId);
        var actorIsOwner = group.OwnerUserId == actorUserId;
        if (actor?.Status != "Active" || (!actorIsOwner && actor.MemberRole != "Moderator"))
            throw new StudyGroupWorkflowException("forbidden", "Bạn không có quyền quản lý thành viên.");
        if (targetUserId == group.OwnerUserId)
            throw new StudyGroupWorkflowException("owner_protected", "Không thể thay đổi trạng thái của trưởng nhóm.");
        if (targetUserId == actorUserId)
            throw new StudyGroupWorkflowException("self_action", "Bạn không thể thực hiện thao tác này với chính mình.");

        var target = group.GroupMembers.SingleOrDefault(item => item.UserId == targetUserId)
            ?? throw new StudyGroupWorkflowException("member_not_found", "Không tìm thấy thành viên.");
        if (!actorIsOwner && target.MemberRole == "Moderator")
            throw new StudyGroupWorkflowException("forbidden", "Điều phối viên không thể quản lý điều phối viên khác.");

        var now = DateTime.UtcNow;
        switch (action)
        {
            case "approve" when target.Status == "Pending":
                EnsureCapacity(group);
                target.Status = "Active";
                target.JoinedAtUtc = now;
                break;
            case "reject" when target.Status == "Pending":
                target.Status = "Rejected";
                target.JoinedAtUtc = null;
                break;
            case "kick" when target.Status == "Active":
                target.Status = "Left";
                target.JoinedAtUtc = null;
                break;
            case "ban":
                target.Status = "Banned";
                target.JoinedAtUtc = null;
                target.BannedByUserId = actorUserId;
                target.UnbanRequestedAtUtc = null;
                break;
            case "unban" when target.Status == "Banned" && target.UnbanRequestedAtUtc is not null &&
                (target.BannedByUserId == actorUserId || target.BannedByUserId is null && actorIsOwner):
                target.Status = "Left";
                target.MemberRole = "Member";
                target.BannedByUserId = null;
                target.UnbanRequestedAtUtc = null;
                break;
            case "promote" when actorIsOwner && target.Status == "Active":
                target.MemberRole = "Moderator";
                break;
            case "demote" when actorIsOwner && target.Status == "Active":
                target.MemberRole = "Member";
                break;
            default:
                throw new StudyGroupWorkflowException("invalid_action", "Thao tác không phù hợp với trạng thái thành viên hiện tại.");
        }

        if (action is "kick" or "ban")
            await RestorePrivateResourcesAsync(groupId, targetUserId, cancellationToken);
        await data.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

}
