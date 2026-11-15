using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.DTOs.StudyGroups;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using System.Data;
using System.Security.Cryptography;


namespace StudyHub.BLL.Services.StudyGroups;

public sealed class StudyGroupService(ICurrentUser currentUser, IRepository data) : IStudyGroupService
{
    public Task DeleteGroupAsync(string groupId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => DeleteAsyncCore(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId, cancellationToken));

    public async Task<IReadOnlyList<GroupSubjectOptionResponse>> GetSubjectOptionsAsync(
        CancellationToken cancellationToken = default) =>
        (await GetSubjectOptionsAsyncCore(currentUser.UserId, cancellationToken))
            .Select(item => new GroupSubjectOptionResponse(item.Name, item.IsCustom))
            .ToList();

    public async Task<IReadOnlyList<GroupSummaryResponse>> GetGroupsAsync(
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        var groups = await GetVisibleAsyncCore(
            currentUser.UserId, searchText, cancellationToken);
        return groups
            .OrderByDescending(group => group.CurrentMembershipStatus == "Active")
            .ThenByDescending(group => group.CurrentMembershipStatus == "Pending")
            .ThenBy(group => group.Name)
            .Select(ToSummary)
            .ToList();
    }

    public async Task<GroupDetailsResponse?> GetGroupAsync(
        string groupId,
        CancellationToken cancellationToken = default)
    {
        var id = ParseId(groupId, "Mã nhóm không hợp lệ.");
        var group = await GetAsyncCore(id, currentUser.UserId, cancellationToken);
        return group is null ? null : ToDetails(group);
    }

    public async Task<GroupMembershipResponse> JoinAsync(
        string groupId,
        CancellationToken cancellationToken = default) =>
        ToMembership(await ExecuteAsync(() => RequestJoinAsyncCore(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId, cancellationToken)));

    public async Task<GroupMembershipResponse> LeaveOrWithdrawAsync(
        string groupId,
        CancellationToken cancellationToken = default) =>
        ToMembership(await ExecuteAsync(() => LeaveOrWithdrawAsyncCore(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId, cancellationToken)));

    public async Task<GroupMembershipResponse> RequestUnbanAsync(string groupId, CancellationToken cancellationToken = default) =>
        ToMembership(await ExecuteAsync(() => RequestUnbanAsyncCore(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId, cancellationToken)));

    public Task<GroupPostResponse> AddMessageAsync(
        string groupId,
        string content,
        CancellationToken cancellationToken = default) =>
        AddPostAsync(groupId, content, "Message", false, 1000, cancellationToken);

    public Task<GroupPostResponse> AddAnnouncementAsync(
        string groupId,
        string content,
        bool isPinned,
        CancellationToken cancellationToken = default) =>
        AddPostAsync(groupId, content, "Announcement", isPinned, 4000, cancellationToken);

    public async Task<GroupDetailsResponse> CreateGroupAsync(
        CreateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = Required(request.Name, "Tên nhóm là bắt buộc.", 150);
        var subjects = NormalizeSubjects(request.Subjects, request.Subject);
        var meetingFormat = NormalizeChoice(request.MeetingFormat, "Online", ["Online", "Offline", "Hybrid"], "Hình thức học không hợp lệ.");
        var joinMode = request.IsPublic
            ? NormalizeChoice(request.JoinMode, "Approval", ["Open", "Approval"], "Cách tham gia không hợp lệ.")
            : "InviteOnly";
        if (request.MaxMembers is not null and (< 2 or > 500))
            throw new StudyGroupException("invalid_member_limit", "Số thành viên tối đa phải từ 2 đến 500.");
        var maxMembers = request.MaxMembers;
        ValidateLength(request.Description, 2000, "Mô tả không được dài quá 2000 ký tự.");
        ValidateLength(request.Goal, 500, "Mục tiêu không được dài quá 500 ký tự.");
        ValidateLength(request.MeetingSchedule, 250, "Lịch học không được dài quá 250 ký tự.");
        ValidateLength(request.Rules, 2000, "Nội quy không được dài quá 2000 ký tự.");

        var created = await ExecuteAsync(() => CreateAsyncCore(new CreateStudyGroupData
        {
            OwnerUserId = currentUser.UserId,
            Name = name,
            Subjects = subjects,
            Description = request.Description?.Trim(),
            Goal = request.Goal?.Trim(),
            MeetingFormat = meetingFormat,
            MeetingSchedule = request.MeetingSchedule?.Trim(),
            Rules = request.Rules?.Trim(),
            Visibility = request.IsPublic ? "Public" : "Private",
            JoinMode = joinMode,
            MaxMembers = maxMembers
        }, cancellationToken));
        return ToDetails(created);
    }

    public async Task<GroupDetailsResponse> UpdateGroupAsync(
        string groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = Required(request.Name, "Tên nhóm là bắt buộc.", 150);
        var subjects = NormalizeSubjects(request.Subjects, request.Subject);
        var meetingFormat = NormalizeChoice(request.MeetingFormat, "Online", ["Online", "Offline", "Hybrid"], "Hình thức học không hợp lệ.");
        var joinMode = request.IsPublic
            ? NormalizeChoice(request.JoinMode, "Approval", ["Open", "Approval"], "Cách tham gia không hợp lệ.")
            : "InviteOnly";
        if (request.MaxMembers is not null and (< 2 or > 500))
            throw new StudyGroupException("invalid_member_limit", "Số thành viên tối đa phải từ 2 đến 500.");
        var maxMembers = request.MaxMembers;
        ValidateLength(request.Description, 2000, "Mô tả không được dài quá 2000 ký tự.");
        ValidateLength(request.Goal, 500, "Mục tiêu không được dài quá 500 ký tự.");
        ValidateLength(request.MeetingSchedule, 250, "Lịch học không được dài quá 250 ký tự.");
        ValidateLength(request.Rules, 2000, "Nội quy không được dài quá 2000 ký tự.");

        var updated = await ExecuteAsync(() => UpdateAsyncCore(
            ParseId(groupId, "Mã nhóm không hợp lệ."),
            currentUser.UserId,
            new UpdateStudyGroupData
            {
                Name = name,
                Subjects = subjects,
                Description = request.Description?.Trim(),
                Goal = request.Goal?.Trim(),
                MeetingFormat = meetingFormat,
                MeetingSchedule = request.MeetingSchedule?.Trim(),
                Rules = request.Rules?.Trim(),
                Visibility = request.IsPublic ? "Public" : "Private",
                JoinMode = joinMode,
                MaxMembers = maxMembers
            },
            cancellationToken));
        return ToDetails(updated);
    }

    public Task ManageMemberAsync(
        string groupId,
        string targetUserId,
        string action,
        CancellationToken cancellationToken = default)
    {
        var normalizedAction = NormalizeChoice(action, string.Empty,
            ["approve", "reject", "kick", "ban", "unban", "promote", "demote"], "Thao tác thành viên không hợp lệ.");
        return ExecuteAsync(() => ManageMemberAsyncCore(
            ParseId(groupId, "Mã nhóm không hợp lệ."),
            currentUser.UserId,
            ParseId(targetUserId, "Mã thành viên không hợp lệ."),
            normalizedAction,
            cancellationToken));
    }

    public async Task<GroupInviteResponse> CreateInviteAsync(
        string groupId,
        int expiresInDays,
        int maxUses,
        CancellationToken cancellationToken = default)
    {
        if (expiresInDays is < 1 or > 30)
            throw new StudyGroupException("invalid_expiry", "Lời mời phải có hạn từ 1 đến 30 ngày.");
        if (maxUses is < 1 or > 1000)
            throw new StudyGroupException("invalid_uses", "Số lượt dùng phải từ 1 đến 1000.");
        var invite = await ExecuteAsync(() => CreateInviteAsyncCore(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId,
            expiresInDays, maxUses, cancellationToken));
        return ToInvite(invite);
    }

    public async Task<GroupInviteResponse?> GetInviteAsync(
        string inviteCode,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeInviteCode(inviteCode);
        var invite = await GetInviteAsyncCore(normalized, cancellationToken);
        return invite is null ? null : ToInvite(invite);
    }

    public async Task<GroupMembershipResponse> AcceptInviteAsync(
        string inviteCode,
        CancellationToken cancellationToken = default) =>
        ToMembership(await ExecuteAsync(() => AcceptInviteAsyncCore(
            NormalizeInviteCode(inviteCode), currentUser.UserId, cancellationToken)));

    public Task<bool> CanAccessChatAsync(
        string groupId,
        CancellationToken cancellationToken = default) =>
        IsActiveMemberAsyncCore(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId, cancellationToken);

    private async Task<GroupPostResponse> AddPostAsync(
        string groupId,
        string content,
        string postType,
        bool isPinned,
        int maxLength,
        CancellationToken cancellationToken)
    {
        var normalized = Required(content, "Nội dung không được để trống.", maxLength);
        var post = await ExecuteAsync(() => AddPostAsyncCore(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId,
            normalized, postType, isPinned, cancellationToken));
        return ToPost(post);
    }

    private GroupSummaryResponse ToSummary(StudyGroupData group) => new(
        group.Id,
        group.Name,
        group.Subject,
        group.SubjectCssClass,
        group.Subjects.Select(item => new GroupSubjectResponse(item.Name, item.CssClass, item.IsCustom)).ToList(),
        group.Description,
        group.Goal,
        group.MeetingFormat,
        group.MeetingSchedule,
        ActiveMemberCount(group),
        group.MaxMembers,
        CanManage(group) ? group.Members.Count(member => member.Status == "Pending") : 0,
        group.CurrentMembershipStatus,
        group.CurrentMemberRole,
        group.CurrentMembershipStatus == "Active",
        group.OwnerUserId == currentUser.UserId.ToString(),
        group.IsPublic,
        group.JoinMode);

    private GroupDetailsResponse ToDetails(StudyGroupData group)
    {
        var isMember = group.CurrentMembershipStatus == "Active";
        var isOwner = group.OwnerUserId == currentUser.UserId.ToString();
        var canManage = isMember && (isOwner || group.CurrentMemberRole == "Moderator");
        return new GroupDetailsResponse(
            group.Id,
            long.Parse(group.OwnerUserId),
            group.Name,
            group.Subject,
            group.SubjectCssClass,
            group.Subjects.Select(item => new GroupSubjectResponse(item.Name, item.CssClass, item.IsCustom)).ToList(),
            group.Description,
            group.Goal,
            group.MeetingFormat,
            group.MeetingSchedule,
            group.Rules,
            ActiveMemberCount(group),
            group.MaxMembers,
            group.CurrentMembershipStatus,
            group.CurrentMemberRole,
            isMember,
            isOwner,
            canManage,
            group.IsPublic,
            group.JoinMode,
            group.Members.Select(member => new GroupMemberResponse(
                member.UserId,
                member.DisplayName,
                member.AvatarUrl,
                member.Role,
                member.Status,
                member.RequestedAt,
                member.JoinedAt,
                member.UserId == currentUser.UserId.ToString(),
                member.HasPendingUnbanRequest,
                member.BannedByUserId)).ToList(),
            group.Posts.Where(post => post.PostType == "Announcement").OrderByDescending(post => post.IsPinned).ThenByDescending(post => post.SentAt).Select(ToPost).ToList(),
            group.Posts.Where(post => post.PostType == "Message").OrderBy(post => post.SentAt).Select(ToPost).ToList(),
            group.Resources.Select(item => new GroupResourceResponse(item.Id, item.Title, item.ResourceType, item.Url)).ToList(),
            group.HasPendingUnbanRequest);
    }

    private GroupPostResponse ToPost(GroupPostData post) => new(
        post.Id,
        post.UserId,
        post.AuthorName,
        post.AuthorAvatarUrl,
        post.Content,
        post.PostType,
        post.IsPinned,
        post.SentAt,
        post.UserId == currentUser.UserId.ToString());

    private static GroupMembershipResponse ToMembership(GroupMembershipData membership) =>
        new(membership.GroupId, membership.Status, membership.Message);

    private static GroupInviteResponse ToInvite(GroupInviteData invite) => new(
        invite.InviteCode,
        invite.GroupId,
        invite.GroupName,
        invite.Subject,
        invite.ExpiresAt,
        invite.MaxUses,
        invite.UseCount);

    private static int ActiveMemberCount(StudyGroupData group) =>
        group.Members.Count(member => member.Status == "Active");

    private bool CanManage(StudyGroupData group) =>
        group.CurrentMembershipStatus == "Active" &&
        (group.OwnerUserId == currentUser.UserId.ToString() || group.CurrentMemberRole == "Moderator");

    private static long ParseId(string value, string message) =>
        long.TryParse(value, out var id) && id > 0
            ? id
            : throw new StudyGroupException("invalid_id", message);

    private static string Required(string? value, string message, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new StudyGroupException("required", message);
        if (normalized.Length > maxLength)
            throw new StudyGroupException("too_long", $"Nội dung không được dài quá {maxLength} ký tự.");
        return normalized;
    }

    private static IReadOnlyList<string> NormalizeSubjects(
        IReadOnlyList<string>? subjects,
        string? legacySubject)
    {
        var values = (subjects ?? [])
            .Append(legacySubject)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (values.Count == 0)
            throw new StudyGroupException("required", "Hãy chọn hoặc thêm ít nhất một môn học.");
        if (values.Count > 10)
            throw new StudyGroupException("too_many_subjects", "Mỗi nhóm được chọn tối đa 10 môn học.");
        if (values.Any(value => value.Length > 150))
            throw new StudyGroupException("too_long", "Tên môn học không được dài quá 150 ký tự.");

        return values;
    }

    private static void ValidateLength(string? value, int maxLength, string message)
    {
        if ((value?.Trim().Length ?? 0) > maxLength) throw new StudyGroupException("too_long", message);
    }

    private static string NormalizeChoice(string? value, string fallback, string[] accepted, string message)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var match = accepted.FirstOrDefault(item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new StudyGroupException("invalid_choice", message);
    }

    private static string NormalizeInviteCode(string inviteCode)
    {
        var normalized = inviteCode?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 64 || normalized.Any(character => !char.IsAsciiHexDigit(character)))
            throw new StudyGroupException("invalid_invite", "Mã lời mời không hợp lệ.");
        return normalized;
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return await data.ExecuteAsync(action); }
        catch (StudyGroupWorkflowException exception) { throw Map(exception); }
        catch (DataConflictException exception) { throw new StudyGroupException("update_conflict", exception.Message, 409); }
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        try { await data.ExecuteAsync(async () => { await action(); return true; }); }
        catch (StudyGroupWorkflowException exception) { throw Map(exception); }
        catch (DataConflictException exception) { throw new StudyGroupException("update_conflict", exception.Message, 409); }
    }

    private static StudyGroupException Map(StudyGroupWorkflowException exception) =>
        new(exception.Code, exception.Message, exception.Code switch
        {
            "not_found" or "member_not_found" or "invite_not_found" => 404,
            "group_full" or "member_limit_too_low" or "update_conflict" => 409,
            "banned" or "forbidden" or "invite_required" or "owner_protected" => 403,
            "invite_expired" => 410,
            _ => 400
        });

    private async Task DeleteAsyncCore(long groupId, long ownerUserId, CancellationToken cancellationToken = default)
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

    private async Task<StudyGroupData> CreateAsyncCore(
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

        return await GetAsyncCore(entity.StudyGroupId, group.OwnerUserId, cancellationToken)
            ?? throw new StudyGroupWorkflowException("create_failed", "Không thể đọc lại nhóm vừa tạo.");
    }

    private async Task<StudyGroupData> UpdateAsyncCore(
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

        return await GetAsyncCore(groupId, ownerUserId, cancellationToken)
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


    private async Task<GroupInviteData> CreateInviteAsyncCore(
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

    private async Task<GroupInviteData?> GetInviteAsyncCore(
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

    private async Task<GroupMembershipData> AcceptInviteAsyncCore(
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



    private async Task<GroupMembershipData> RequestJoinAsyncCore(
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

    private async Task<GroupMembershipData> LeaveOrWithdrawAsyncCore(
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

    private async Task<GroupMembershipData> RequestUnbanAsyncCore(long groupId, long userId, CancellationToken cancellationToken = default)
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

    private async Task ManageMemberAsyncCore(
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



    private async Task<GroupPostData> AddPostAsyncCore(
        long groupId,
        long userId,
        string content,
        string postType,
        bool isPinned,
        CancellationToken cancellationToken = default)
    {
        var membership = await data.Query<GroupMember>(false)
            .Include(item => item.User)
            .Include(item => item.StudyGroup)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && item.UserId == userId, cancellationToken);
        if (membership?.Status != "Active" || membership.StudyGroup.IsArchived)
            throw new StudyGroupWorkflowException("member_required", "Bạn phải là thành viên đang hoạt động của nhóm.");

        var canManage = membership.StudyGroup.OwnerUserId == userId || membership.MemberRole == "Moderator";
        if (postType == "Announcement" && !canManage)
            throw new StudyGroupWorkflowException("forbidden", "Chỉ trưởng nhóm hoặc điều phối viên được đăng thông báo.");

        if (postType == "Announcement" && isPinned)
        {
            await data.Query<GroupPost>(false)
                .Where(item => item.StudyGroupId == groupId && item.PostType == "Announcement" && item.IsPinned)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsPinned, false), cancellationToken);
        }

        var now = DateTime.UtcNow;
        var post = new GroupPost
        {
            StudyGroupId = groupId,
            AuthorUserId = userId,
            Body = content,
            PostType = postType,
            IsPinned = postType == "Announcement" && isPinned,
            IsDeleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await data.AddAsync(post);
        await data.SaveChangesAsync(cancellationToken);
        return new GroupPostData
        {
            Id = post.GroupPostId.ToString(),
            UserId = userId.ToString(),
            AuthorName = membership.User.DisplayName,
            AuthorAvatarUrl = membership.User.AvatarUrl,
            Content = post.Body,
            PostType = post.PostType,
            IsPinned = post.IsPinned,
            SentAt = AsUtc(post.CreatedAtUtc)
        };
    }



    private async Task<IReadOnlyList<GroupSubjectOptionData>> GetSubjectOptionsAsyncCore(
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var catalogNames = await data.Query<Subject>(false).AsNoTracking()
            .Where(subject => subject.IsActive)
            .Select(subject => subject.SubjectName)
            .ToListAsync(cancellationToken);
        var personalNames = await data.Query<UserSubject>(false).AsNoTracking()
            .Where(item => item.UserId == currentUserId && !item.IsArchived && item.CustomSubjectName != null)
            .Select(item => item.CustomSubjectName!)
            .ToListAsync(cancellationToken);
        var previousGroupNames = await data.Query<StudyGroupSubject>(false).AsNoTracking()
            .Where(item => item.AddedByUserId == currentUserId && item.CustomSubjectName != null)
            .Select(item => item.CustomSubjectName!)
            .ToListAsync(cancellationToken);

        var catalogSet = catalogNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return catalogNames
            .Concat(personalNames)
            .Concat(previousGroupNames)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .Select(name => new GroupSubjectOptionData(name, !catalogSet.Contains(name)))
            .ToList();
    }

    private async Task<IReadOnlyList<StudyGroupData>> GetVisibleAsyncCore(
        long currentUserId,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        var query = data.Query<StudyGroup>(false)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(group => !group.IsArchived &&
                (group.Visibility == "Public" || group.GroupMembers.Any(member =>
                    member.UserId == currentUserId &&
                    (member.Status == "Active" || member.Status == "Pending" || member.Status == "Banned"))))
            .Include(group => group.StudyGroupSubjects)
                .ThenInclude(item => item.Subject)
            .Include(group => group.GroupMembers)
                .ThenInclude(member => member.User)
            .AsQueryable();

        var keyword = searchText?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(group =>
                group.GroupName.Contains(keyword) ||
                (group.Description != null && group.Description.Contains(keyword)) ||
                (group.Goal != null && group.Goal.Contains(keyword)) ||
                group.StudyGroupSubjects.Any(item =>
                    (item.Subject != null && item.Subject.SubjectName.Contains(keyword)) ||
                    (item.CustomSubjectName != null && item.CustomSubjectName.Contains(keyword))));
        }

        var groups = await query.OrderBy(group => group.GroupName).ToListAsync(cancellationToken);
        return groups.Select(group => ToDomain(group, currentUserId)).ToList();
    }

    private async Task<StudyGroupData?> GetAsyncCore(
        long groupId,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var group = await data.Query<StudyGroup>(false)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(item => !item.IsArchived && item.StudyGroupId == groupId)
            .Include(item => item.StudyGroupSubjects)
                .ThenInclude(subject => subject.Subject)
            .Include(item => item.GroupMembers)
                .ThenInclude(member => member.User)
            .SingleOrDefaultAsync(cancellationToken);

        if (group is null) return null;

        var currentMembership = group.GroupMembers.SingleOrDefault(member => member.UserId == currentUserId);
        var canPreview = group.Visibility == "Public" || currentMembership?.Status is "Active" or "Pending" or "Banned";
        if (!canPreview) return null;

        var isActiveMember = currentMembership?.Status == "Active";
        var canManage = isActiveMember &&
            (group.OwnerUserId == currentUserId || currentMembership?.MemberRole == "Moderator");

        var result = ToDomain(group, currentUserId);
        if (!isActiveMember)
        {
            result.Members.Clear();
            return result;
        }

        var posts = await data.Query<GroupPost>(false)
            .AsNoTracking()
            .Where(post => post.StudyGroupId == groupId && !post.IsDeleted)
            .Include(post => post.GroupMember)
                .ThenInclude(member => member.User)
            .OrderByDescending(post => post.CreatedAtUtc)
            .Take(120)
            .ToListAsync(cancellationToken);

        result.Posts.AddRange(posts
            .OrderBy(post => post.CreatedAtUtc)
            .Select(ToPost));

        var decks = await data.Query<FlashcardDeck>(false).AsNoTracking()
            .Where(item => item.StudyGroupId == groupId && item.Visibility == "Group" && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(20)
            .Select(item => new GroupResourceData(
                $"deck-{item.DeckId}", item.Title, "Flashcard", $"/Groups/{groupId}/Resources/Flashcard/{item.DeckId}"))
            .ToListAsync(cancellationToken);
        result.Resources.AddRange(decks);

        if (!canManage)
            result.Members.RemoveAll(member => member.Status != "Active");

        return result;
    }

    private Task<bool> IsActiveMemberAsyncCore(
        long groupId,
        long userId,
        CancellationToken cancellationToken = default) =>
        data.Query<GroupMember>(false).AsNoTracking().AnyAsync(
            item => item.StudyGroupId == groupId && item.UserId == userId && item.Status == "Active" && !item.StudyGroup.IsArchived,
            cancellationToken);



    private async Task RestorePrivateResourcesAsync(
    long groupId,
    long userId,
    CancellationToken ct)
{
    // Giữ nguyên cơ chế flashcard hiện có.
    await data.Query<FlashcardDeck>(false)
        .Where(x =>
            x.StudyGroupId == groupId &&
            x.OwnerUserId == userId)
        .ExecuteUpdateAsync(
            setters => setters
                .SetProperty(x => x.StudyGroupId, (long?)null)
                .SetProperty(x => x.Visibility, "Private")
                .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow),
            ct);

    // Khi chủ sở hữu rời, bị kick hoặc bị ban, PDF chỉ bị thu hồi khỏi nhóm;
    // file PDF cá nhân gốc vẫn thuộc về chủ sở hữu.
    var unsharedAtUtc = DateTime.UtcNow;
    var sharedDocumentIds = await data.Query<PdfDocumentShare>(false)
        .Where(share =>
            share.StudyGroupId == groupId &&
            share.IsActive)
        .Join(
            data.Query<PdfDocument>(false).Where(document => document.OwnerUserId == userId),
            share => share.DocumentId,
            document => document.DocumentId,
            (share, document) => document.DocumentId)
        .Distinct()
        .ToListAsync(ct);

    await data.Query<PdfDocumentShare>(false)
        .Where(share =>
            share.StudyGroupId == groupId &&
            share.IsActive &&
            data.Query<PdfDocument>(false).Any(document =>
                document.DocumentId == share.DocumentId &&
                document.OwnerUserId == userId))
        .ExecuteUpdateAsync(
            setters => setters
                .SetProperty(share => share.IsActive, false)
                .SetProperty(share => share.UnsharedAtUtc, unsharedAtUtc),
            ct);

    if (sharedDocumentIds.Count > 0)
    {
        await data.Query<PdfDocument>(false)
            .Where(document => sharedDocumentIds.Contains(document.DocumentId))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    document => document.UpdatedAtUtc,
                    unsharedAtUtc),
                ct);
    }

    // Giữ nguyên xử lý đăng ký buổi học tương lai.
    var now = DateTimeOffset.UtcNow;

    await data.Query<GroupSessionAttendance>(false)
        .Where(x =>
            x.UserId == userId &&
            x.Session.StudyGroupId == groupId &&
            x.Session.Status == "Scheduled" &&
            x.Session.StartsAtUtc > now)
        .ExecuteDeleteAsync(ct);
}



}

public sealed class StudyGroupWorkflowException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
