using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed class StudyGroupService(IRepository repository, ICurrentUser currentUser) : IStudyGroupService
{
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
        var subject = Required(request.Subject, "Môn học là bắt buộc.", 150);
        var meetingFormat = NormalizeChoice(request.MeetingFormat, "Online", ["Online", "Offline", "Hybrid"], "Hình thức học không hợp lệ.");
        var joinMode = request.IsPublic
            ? NormalizeChoice(request.JoinMode, "Approval", ["Open", "Approval"], "Cách tham gia không hợp lệ.")
            : "InviteOnly";
        var maxMembers = request.MaxMembers is >= 2 and <= 500 ? request.MaxMembers : (short)20;
        ValidateLength(request.Description, 2000, "Mô tả không được dài quá 2000 ký tự.");
        ValidateLength(request.Goal, 500, "Mục tiêu không được dài quá 500 ký tự.");
        ValidateLength(request.MeetingSchedule, 250, "Lịch học không được dài quá 250 ký tự.");
        ValidateLength(request.Rules, 2000, "Nội quy không được dài quá 2000 ký tự.");
        ValidateContactUrl(request.ContactUrl);

        var created = await ExecuteAsync(() => repository.StudyGroups.CreateAsync(new CreateStudyGroupData
        {
            OwnerUserId = currentUser.UserId,
            Name = name,
            Subject = subject,
            Description = request.Description?.Trim(),
            Goal = request.Goal?.Trim(),
            MeetingFormat = meetingFormat,
            MeetingSchedule = request.MeetingSchedule?.Trim(),
            ContactUrl = request.ContactUrl?.Trim(),
            Rules = request.Rules?.Trim(),
            Visibility = request.IsPublic ? "Public" : "Private",
            JoinMode = joinMode,
            MaxMembers = maxMembers
        }, cancellationToken));
        return ToDetails(created);
    }

    public Task ManageMemberAsync(
        string groupId,
        string targetUserId,
        string action,
        CancellationToken cancellationToken = default)
    {
        var normalizedAction = NormalizeChoice(action, string.Empty,
            ["approve", "reject", "kick", "ban", "promote", "demote"], "Thao tác thành viên không hợp lệ.");
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
            group.Description,
            group.Goal,
            group.MeetingFormat,
            group.MeetingSchedule,
            group.ContactUrl,
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
                member.UserId == currentUser.UserId.ToString())).ToList(),
            group.Posts.Where(post => post.PostType == "Announcement").OrderByDescending(post => post.IsPinned).ThenByDescending(post => post.SentAt).Select(ToPost).ToList(),
            group.Posts.Where(post => post.PostType == "Message").OrderBy(post => post.SentAt).Select(ToPost).ToList(),
            group.Resources.Select(item => new GroupResourceResponse(item.Id, item.Title, item.ResourceType, item.Url)).ToList());
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

    private static void ValidateContactUrl(string? contactUrl)
    {
        if (string.IsNullOrWhiteSpace(contactUrl)) return;
        if (contactUrl.Trim().Length > 2048 ||
            !Uri.TryCreate(contactUrl.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https" or "mailto"))
            throw new StudyGroupException("invalid_contact", "Liên kết liên hệ không hợp lệ.");
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
            "group_full" => 409,
            "banned" or "forbidden" or "invite_required" or "owner_protected" => 403,
            "invite_expired" => 410,
            _ => 400
        });
}
