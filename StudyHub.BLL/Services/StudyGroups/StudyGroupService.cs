using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed class StudyGroupService(IRepository repository, ICurrentUser currentUser) : IStudyGroupService
{
    public async Task<IReadOnlyList<GroupSubjectOptionResponse>> GetSubjectOptionsAsync(
        CancellationToken cancellationToken = default) =>
        (await repository.StudyGroups.GetSubjectOptionsAsync(currentUser.UserId, cancellationToken))
            .Select(item => new GroupSubjectOptionResponse(item.Name, item.IsCustom))
            .ToList();

    public async Task<IReadOnlyList<GroupSummaryResponse>> GetGroupsAsync(
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        var groups = await repository.StudyGroups.GetVisibleAsync(
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
        var group = await repository.StudyGroups.GetAsync(id, currentUser.UserId, cancellationToken);
        return group is null ? null : ToDetails(group);
    }

    public async Task<GroupMembershipResponse> JoinAsync(
        string groupId,
        CancellationToken cancellationToken = default) =>
        ToMembership(await ExecuteAsync(() => repository.StudyGroups.RequestJoinAsync(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId, cancellationToken)));

    public async Task<GroupMembershipResponse> LeaveOrWithdrawAsync(
        string groupId,
        CancellationToken cancellationToken = default) =>
        ToMembership(await ExecuteAsync(() => repository.StudyGroups.LeaveOrWithdrawAsync(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId, cancellationToken)));

    public async Task<GroupMembershipResponse> RequestUnbanAsync(string groupId, CancellationToken cancellationToken = default) =>
        ToMembership(await ExecuteAsync(() => repository.StudyGroups.RequestUnbanAsync(
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

        var created = await ExecuteAsync(() => repository.StudyGroups.CreateAsync(new CreateStudyGroupData
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

        var updated = await ExecuteAsync(() => repository.StudyGroups.UpdateAsync(
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
        return ExecuteAsync(() => repository.StudyGroups.ManageMemberAsync(
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
        var invite = await ExecuteAsync(() => repository.StudyGroups.CreateInviteAsync(
            ParseId(groupId, "Mã nhóm không hợp lệ."), currentUser.UserId,
            expiresInDays, maxUses, cancellationToken));
        return ToInvite(invite);
    }

    public async Task<GroupInviteResponse?> GetInviteAsync(
        string inviteCode,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeInviteCode(inviteCode);
        var invite = await repository.StudyGroups.GetInviteAsync(normalized, cancellationToken);
        return invite is null ? null : ToInvite(invite);
    }

    public async Task<GroupMembershipResponse> AcceptInviteAsync(
        string inviteCode,
        CancellationToken cancellationToken = default) =>
        ToMembership(await ExecuteAsync(() => repository.StudyGroups.AcceptInviteAsync(
            NormalizeInviteCode(inviteCode), currentUser.UserId, cancellationToken)));

    public Task<bool> CanAccessChatAsync(
        string groupId,
        CancellationToken cancellationToken = default) =>
        repository.StudyGroups.IsActiveMemberAsync(
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
        var post = await ExecuteAsync(() => repository.StudyGroups.AddPostAsync(
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

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (StudyGroupRepositoryException exception) { throw Map(exception); }
    }

    private static async Task ExecuteAsync(Func<Task> action)
    {
        try { await action(); }
        catch (StudyGroupRepositoryException exception) { throw Map(exception); }
    }

    private static StudyGroupException Map(StudyGroupRepositoryException exception) =>
        new(exception.Code, exception.Message, exception.Code switch
        {
            "not_found" or "member_not_found" or "invite_not_found" => 404,
            "group_full" or "member_limit_too_low" or "update_conflict" => 409,
            "banned" or "forbidden" or "invite_required" or "owner_protected" => 403,
            "invite_expired" => 410,
            _ => 400
        });
}
