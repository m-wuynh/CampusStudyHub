using StudyHub.DAL.Entities.Pdf;
using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using StudyHub.BLL.Services.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed partial class StudyGroupWorkflow(IRepository data) : IStudyGroupWorkflow
{
    public async Task DeleteAsync(long groupId, long ownerUserId, CancellationToken cancellationToken = default)
    {
        var group = await data.Query<StudyGroup>(false).SingleOrDefaultAsync(
            item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupWorkflowException("not_found", "Nhóm học tập không tồn tại.");
        if (group.OwnerUserId != ownerUserId)
            throw new StudyGroupWorkflowException("forbidden", "Chỉ chủ sở hữu mới có thể xóa nhóm học tập.");

        // Preserve related data while disabling group access, membership actions and invites.
        group.IsArchived = true;
        group.UpdatedAtUtc = DateTime.UtcNow;
        await data.SaveChangesAsync(cancellationToken);
    }

    public async Task<StudyGroupData> CreateAsync(
        CreateStudyGroupData group,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await data.BeginTransactionAsync(cancellationToken);
        var catalogSubjects = await data.Query<Subject>(false)
            .Where(item => item.IsActive)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var entity = new StudyGroup
        {
            OwnerUserId = group.OwnerUserId,
            GroupName = group.Name,
            Description = NullIfWhiteSpace(group.Description),
            Goal = NullIfWhiteSpace(group.Goal),
            MeetingFormat = group.MeetingFormat,
            MeetingSchedule = NullIfWhiteSpace(group.MeetingSchedule),
            Rules = NullIfWhiteSpace(group.Rules),
            Visibility = group.Visibility,
            JoinMode = group.JoinMode,
            MaxMembers = group.MaxMembers,
            IsArchived = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await data.AddAsync(entity);
        await data.SaveChangesAsync(cancellationToken);

        foreach (var subjectName in group.Subjects)
        {
            var catalogSubject = catalogSubjects.FirstOrDefault(item =>
                item.SubjectName.Equals(subjectName, StringComparison.OrdinalIgnoreCase));
            await data.AddAsync(new StudyGroupSubject
            {
                StudyGroupId = entity.StudyGroupId,
                SubjectId = catalogSubject?.SubjectId,
                CustomSubjectName = catalogSubject is null ? subjectName : null,
                AddedByUserId = group.OwnerUserId,
                CreatedAtUtc = now
            });
        }

        await data.AddAsync(new GroupMember
        {
            StudyGroupId = entity.StudyGroupId,
            UserId = group.OwnerUserId,
            MemberRole = "Moderator",
            Status = "Active",
            RequestedAtUtc = now,
            JoinedAtUtc = now
        });
        await data.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(entity.StudyGroupId, group.OwnerUserId, cancellationToken)
            ?? throw new StudyGroupWorkflowException("create_failed", "Không thể đọc lại nhóm vừa tạo.");
    }

    public async Task<StudyGroupData> UpdateAsync(
        long groupId,
        long ownerUserId,
        UpdateStudyGroupData group,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await data.BeginTransactionAsync(cancellationToken);

        var entity = await data.Query<StudyGroup>(false)
            .Include(item => item.StudyGroupSubjects)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupWorkflowException("not_found", "Nhóm học tập không tồn tại.");

        if (entity.OwnerUserId != ownerUserId)
            throw new StudyGroupWorkflowException("forbidden", "Chỉ trưởng nhóm mới có thể chỉnh sửa nhóm học tập.");

        var activeMemberCount = await data.Query<GroupMember>(false).CountAsync(
            member => member.StudyGroupId == groupId && member.Status == "Active",
            cancellationToken);
        if (group.MaxMembers is not null && group.MaxMembers < activeMemberCount)
            throw new StudyGroupWorkflowException(
                "member_limit_too_low",
                $"Số thành viên tối đa phải từ {activeMemberCount} trở lên vì nhóm đang có {activeMemberCount} thành viên.");

        var catalogSubjects = await data.Query<Subject>(false)
            .Where(item => item.IsActive)
            .ToListAsync(cancellationToken);

        entity.GroupName = group.Name;
        entity.Description = NullIfWhiteSpace(group.Description);
        entity.Goal = NullIfWhiteSpace(group.Goal);
        entity.MeetingFormat = group.MeetingFormat;
        entity.MeetingSchedule = NullIfWhiteSpace(group.MeetingSchedule);
        entity.Rules = NullIfWhiteSpace(group.Rules);
        entity.Visibility = group.Visibility;
        entity.JoinMode = group.JoinMode;
        entity.MaxMembers = group.MaxMembers;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        data.RemoveRange(entity.StudyGroupSubjects);
        await data.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var subjectName in group.Subjects)
        {
            var catalogSubject = catalogSubjects.FirstOrDefault(item =>
                item.SubjectName.Equals(subjectName, StringComparison.OrdinalIgnoreCase));
            await data.AddAsync(new StudyGroupSubject
            {
                StudyGroupId = entity.StudyGroupId,
                SubjectId = catalogSubject?.SubjectId,
                CustomSubjectName = catalogSubject is null ? subjectName : null,
                AddedByUserId = ownerUserId,
                CreatedAtUtc = now
            });
        }

        await data.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(groupId, ownerUserId, cancellationToken)
            ?? throw new StudyGroupWorkflowException("update_failed", "Không thể đọc lại nhóm vừa cập nhật.");
    }

    private static StudyGroupData ToDomain(StudyGroup group, long currentUserId)
    {
        var membership = group.GroupMembers.SingleOrDefault(member => member.UserId == currentUserId);
        var result = new StudyGroupData
        {
            Id = group.StudyGroupId.ToString(),
            Name = group.GroupName,
            Description = group.Description ?? string.Empty,
            Goal = group.Goal ?? string.Empty,
            MeetingFormat = group.MeetingFormat,
            MeetingSchedule = group.MeetingSchedule ?? string.Empty,
            Rules = group.Rules ?? string.Empty,
            IsPublic = group.Visibility == "Public",
            JoinMode = group.JoinMode,
            MaxMembers = group.MaxMembers,
            OwnerUserId = group.OwnerUserId.ToString(),
            CreatedAt = AsUtc(group.CreatedAtUtc),
            CurrentMembershipStatus = membership?.Status,
            CurrentMemberRole = membership?.MemberRole,
            HasPendingUnbanRequest = membership?.UnbanRequestedAtUtc is not null
        };

        result.Subjects.AddRange(group.StudyGroupSubjects
            .Select(item => new
            {
                Name = item.Subject?.SubjectName ?? item.CustomSubjectName ?? "Môn học",
                IsCustom = item.SubjectId is null
            })
            .OrderBy(item => item.Name)
            .Select(item => new GroupSubjectData(item.Name, GetSubjectCssClass(item.Name), item.IsCustom)));

        result.Members.AddRange(group.GroupMembers
            .Where(member => member.Status is "Active" or "Pending" ||
                member.Status == "Banned" && member.UnbanRequestedAtUtc != null)
            .OrderBy(member => member.Status == "Pending")
            .ThenBy(member => member.User.DisplayName)
            .Select(member => new GroupMemberData
            {
                UserId = member.UserId.ToString(),
                DisplayName = member.User.DisplayName,
                AvatarUrl = member.User.AvatarUrl,
                Role = group.OwnerUserId == member.UserId ? "Owner" : member.MemberRole,
                Status = member.Status,
                HasPendingUnbanRequest = member.UnbanRequestedAtUtc is not null,
                BannedByUserId = member.BannedByUserId?.ToString(),
                RequestedAt = AsUtc(member.RequestedAtUtc),
                JoinedAt = member.JoinedAtUtc.HasValue ? AsUtc(member.JoinedAtUtc.Value) : null
            }));
        return result;
    }

    private static GroupPostData ToPost(GroupPost post) => new()
    {
        Id = post.GroupPostId.ToString(),
        UserId = post.AuthorUserId.ToString(),
        AuthorName = post.GroupMember.User.DisplayName,
        AuthorAvatarUrl = post.GroupMember.User.AvatarUrl,
        Content = post.Body,
        PostType = post.PostType,
        IsPinned = post.IsPinned,
        SentAt = AsUtc(post.CreatedAtUtc)
    };

    private static GroupInviteData ToInvite(GroupInvite invite, StudyGroup group) => new(
        invite.InviteCode,
        group.StudyGroupId.ToString(),
        group.GroupName,
        GetSubjectNames(group),
        AsUtc(invite.ExpiresAtUtc),
        invite.MaxUses,
        invite.UseCount);

    private static void EnsureCapacity(StudyGroup group)
    {
        if (group.MaxMembers is { } maxMembers &&
            group.GroupMembers.Count(member => member.Status == "Active") >= maxMembers)
            throw new StudyGroupWorkflowException("group_full", "Nhóm đã đủ số thành viên tối đa.");
    }

    private static bool InviteIsValid(GroupInvite invite) =>
        !invite.IsRevoked && invite.ExpiresAtUtc > DateTime.UtcNow && invite.UseCount < invite.MaxUses;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static string GetSubjectNames(StudyGroup group)
    {
        var names = group.StudyGroupSubjects
            .Select(item => item.Subject?.SubjectName ?? item.CustomSubjectName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name)
            .ToList();
        return names.Count == 0 ? "Học tập" : string.Join(", ", names);
    }

    private static string GetSubjectCssClass(string? subject) => subject?.Trim().ToLowerInvariant() switch
    {
        "vật lý" => "subject-vatly",
        "hóa học" => "subject-hoahoc",
        "lịch sử" => "subject-lichsu",
        "sinh học" => "subject-sinhhoc",
        "tiếng anh" => "subject-tienganh",
        _ => "subject-toan"
    };
}
