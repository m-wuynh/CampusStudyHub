using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.DAL.Repositories.StudyGroups;

public sealed class SqlStudyGroupRepository(StudyHubDbContext context) : IStudyGroupRepository
{
    public async Task<IReadOnlyList<GroupSubjectOptionData>> GetSubjectOptionsAsync(
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var catalogNames = await context.Subjects.AsNoTracking()
            .Where(subject => subject.IsActive)
            .Select(subject => subject.SubjectName)
            .ToListAsync(cancellationToken);
        var personalNames = await context.UserSubjects.AsNoTracking()
            .Where(item => item.UserId == currentUserId && !item.IsArchived && item.CustomSubjectName != null)
            .Select(item => item.CustomSubjectName!)
            .ToListAsync(cancellationToken);
        var previousGroupNames = await context.StudyGroupSubjects.AsNoTracking()
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

    public async Task<IReadOnlyList<StudyGroupData>> GetVisibleAsync(
        long currentUserId,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.StudyGroups
            .AsNoTracking()
            .AsSplitQuery()
            .Where(group => !group.IsArchived &&
                (group.Visibility == "Public" || group.GroupMembers.Any(member =>
                    member.UserId == currentUserId &&
                    (member.Status == "Active" || member.Status == "Pending"))))
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

    public async Task<StudyGroupData?> GetAsync(
        long groupId,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var group = await context.StudyGroups
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
        var canPreview = group.Visibility == "Public" || currentMembership?.Status is "Active" or "Pending";
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

        var posts = await context.GroupPosts
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

        var documents = await context.Documents.AsNoTracking()
            .Where(item => item.StudyGroupId == groupId && item.Visibility == "Group" && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .Select(item => new GroupResourceData(
                $"document-{item.DocumentId}", item.Title, "Document", item.ExternalUrl))
            .ToListAsync(cancellationToken);
        result.Resources.AddRange(documents);

        var notes = await context.Notes.AsNoTracking()
            .Where(item => item.StudyGroupId == groupId && item.Visibility == "Group" && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(20)
            .Select(item => new GroupResourceData(
                $"note-{item.NoteId}", item.Title, "Note", $"/Notes/Reader/{item.NoteId}"))
            .ToListAsync(cancellationToken);
        result.Resources.AddRange(notes);

        var decks = await context.FlashcardDecks.AsNoTracking()
            .Where(item => item.StudyGroupId == groupId && item.Visibility == "Group" && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(20)
            .Select(item => new GroupResourceData(
                $"deck-{item.DeckId}", item.Title, "Flashcard", "/Flashcards"))
            .ToListAsync(cancellationToken);
        result.Resources.AddRange(decks);

        if (!canManage)
            result.Members.RemoveAll(member => member.Status != "Active");

        return result;
    }

    public async Task<StudyGroupData> CreateAsync(
        CreateStudyGroupData group,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var catalogSubjects = await context.Subjects
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
            ContactUrl = NullIfWhiteSpace(group.ContactUrl),
            Rules = NullIfWhiteSpace(group.Rules),
            Visibility = group.Visibility,
            JoinMode = group.JoinMode,
            MaxMembers = group.MaxMembers,
            IsArchived = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        context.StudyGroups.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var subjectName in group.Subjects)
        {
            var catalogSubject = catalogSubjects.FirstOrDefault(item =>
                item.SubjectName.Equals(subjectName, StringComparison.OrdinalIgnoreCase));
            context.StudyGroupSubjects.Add(new StudyGroupSubject
            {
                StudyGroupId = entity.StudyGroupId,
                SubjectId = catalogSubject?.SubjectId,
                CustomSubjectName = catalogSubject is null ? subjectName : null,
                AddedByUserId = group.OwnerUserId,
                CreatedAtUtc = now
            });
        }

        context.GroupMembers.Add(new GroupMember
        {
            StudyGroupId = entity.StudyGroupId,
            UserId = group.OwnerUserId,
            MemberRole = "Moderator",
            Status = "Active",
            RequestedAtUtc = now,
            JoinedAtUtc = now
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(entity.StudyGroupId, group.OwnerUserId, cancellationToken)
            ?? throw new StudyGroupRepositoryException("create_failed", "Không thể đọc lại nhóm vừa tạo.");
    }

    public async Task<StudyGroupData> UpdateAsync(
        long groupId,
        long ownerUserId,
        UpdateStudyGroupData group,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await context.StudyGroups
                .Include(item => item.StudyGroupSubjects)
                .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
                ?? throw new StudyGroupRepositoryException("not_found", "Nhóm học tập không tồn tại.");

            if (entity.OwnerUserId != ownerUserId)
                throw new StudyGroupRepositoryException("forbidden", "Chỉ trưởng nhóm mới có thể chỉnh sửa nhóm học tập.");

            var activeMemberCount = await context.GroupMembers.CountAsync(
                member => member.StudyGroupId == groupId && member.Status == "Active",
                cancellationToken);
            if (group.MaxMembers < activeMemberCount)
                throw new StudyGroupRepositoryException(
                    "member_limit_too_low",
                    $"Số thành viên tối đa phải từ {activeMemberCount} trở lên vì nhóm đang có {activeMemberCount} thành viên.");

            var catalogSubjects = await context.Subjects
                .Where(item => item.IsActive)
                .ToListAsync(cancellationToken);

            entity.GroupName = group.Name;
            entity.Description = NullIfWhiteSpace(group.Description);
            entity.Goal = NullIfWhiteSpace(group.Goal);
            entity.MeetingFormat = group.MeetingFormat;
            entity.MeetingSchedule = NullIfWhiteSpace(group.MeetingSchedule);
            entity.ContactUrl = NullIfWhiteSpace(group.ContactUrl);
            entity.Rules = NullIfWhiteSpace(group.Rules);
            entity.Visibility = group.Visibility;
            entity.JoinMode = group.JoinMode;
            entity.MaxMembers = group.MaxMembers;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            context.StudyGroupSubjects.RemoveRange(entity.StudyGroupSubjects);
            await context.SaveChangesAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var subjectName in group.Subjects)
            {
                var catalogSubject = catalogSubjects.FirstOrDefault(item =>
                    item.SubjectName.Equals(subjectName, StringComparison.OrdinalIgnoreCase));
                context.StudyGroupSubjects.Add(new StudyGroupSubject
                {
                    StudyGroupId = entity.StudyGroupId,
                    SubjectId = catalogSubject?.SubjectId,
                    CustomSubjectName = catalogSubject is null ? subjectName : null,
                    AddedByUserId = ownerUserId,
                    CreatedAtUtc = now
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StudyGroupRepositoryException(
                "update_conflict",
                "Nhóm vừa được thay đổi ở nơi khác. Hãy tải lại trang và thử lại.");
        }

        return await GetAsync(groupId, ownerUserId, cancellationToken)
            ?? throw new StudyGroupRepositoryException("update_failed", "Không thể đọc lại nhóm vừa cập nhật.");
    }

    public async Task<GroupMembershipData> RequestJoinAsync(
        long groupId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var group = await context.StudyGroups
            .Include(item => item.GroupMembers)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupRepositoryException("not_found", "Không tìm thấy nhóm học tập.");

        if (group.Visibility != "Public" || group.JoinMode == "InviteOnly")
            throw new StudyGroupRepositoryException("invite_required", "Nhóm này chỉ cho phép tham gia bằng lời mời.");

        var membership = group.GroupMembers.SingleOrDefault(item => item.UserId == userId);
        if (membership?.Status == "Banned")
            throw new StudyGroupRepositoryException("banned", "Bạn đã bị cấm tham gia nhóm này.");
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
            context.GroupMembers.Add(membership);
        }
        else
        {
            membership.MemberRole = "Member";
            membership.Status = status;
            membership.RequestedAtUtc = now;
            membership.JoinedAtUtc = status == "Active" ? now : null;
        }

        await context.SaveChangesAsync(cancellationToken);
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
        var group = await context.StudyGroups
            .Include(item => item.GroupMembers)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupRepositoryException("not_found", "Không tìm thấy nhóm học tập.");
        if (group.OwnerUserId == userId)
            throw new StudyGroupRepositoryException("owner_cannot_leave", "Trưởng nhóm phải chuyển quyền hoặc lưu trữ nhóm trước khi rời.");

        var membership = group.GroupMembers.SingleOrDefault(item => item.UserId == userId);
        if (membership is null || membership.Status is "Left" or "Rejected")
            return new GroupMembershipData(groupId.ToString(), "None", "Bạn hiện không tham gia nhóm.");
        if (membership.Status == "Banned")
            throw new StudyGroupRepositoryException("banned", "Bạn đã bị cấm khỏi nhóm này.");

        var wasPending = membership.Status == "Pending";
        membership.Status = "Left";
        membership.JoinedAtUtc = null;
        await context.SaveChangesAsync(cancellationToken);
        return new GroupMembershipData(
            groupId.ToString(),
            "Left",
            wasPending ? "Đã rút yêu cầu tham gia." : "Bạn đã rời nhóm.");
    }

    public async Task<GroupPostData> AddPostAsync(
        long groupId,
        long userId,
        string content,
        string postType,
        bool isPinned,
        CancellationToken cancellationToken = default)
    {
        var membership = await context.GroupMembers
            .Include(item => item.User)
            .Include(item => item.StudyGroup)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && item.UserId == userId, cancellationToken);
        if (membership?.Status != "Active" || membership.StudyGroup.IsArchived)
            throw new StudyGroupRepositoryException("member_required", "Bạn phải là thành viên đang hoạt động của nhóm.");

        var canManage = membership.StudyGroup.OwnerUserId == userId || membership.MemberRole == "Moderator";
        if (postType == "Announcement" && !canManage)
            throw new StudyGroupRepositoryException("forbidden", "Chỉ trưởng nhóm hoặc điều phối viên được đăng thông báo.");

        if (postType == "Announcement" && isPinned)
        {
            await context.GroupPosts
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
        context.GroupPosts.Add(post);
        await context.SaveChangesAsync(cancellationToken);
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

    public async Task ManageMemberAsync(
        long groupId,
        long actorUserId,
        long targetUserId,
        string action,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var group = await context.StudyGroups
            .Include(item => item.GroupMembers)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupRepositoryException("not_found", "Không tìm thấy nhóm học tập.");
        var actor = group.GroupMembers.SingleOrDefault(item => item.UserId == actorUserId);
        var actorIsOwner = group.OwnerUserId == actorUserId;
        if (actor?.Status != "Active" || (!actorIsOwner && actor.MemberRole != "Moderator"))
            throw new StudyGroupRepositoryException("forbidden", "Bạn không có quyền quản lý thành viên.");
        if (targetUserId == group.OwnerUserId)
            throw new StudyGroupRepositoryException("owner_protected", "Không thể thay đổi trạng thái của trưởng nhóm.");
        if (targetUserId == actorUserId)
            throw new StudyGroupRepositoryException("self_action", "Bạn không thể thực hiện thao tác này với chính mình.");

        var target = group.GroupMembers.SingleOrDefault(item => item.UserId == targetUserId)
            ?? throw new StudyGroupRepositoryException("member_not_found", "Không tìm thấy thành viên.");
        if (!actorIsOwner && target.MemberRole == "Moderator")
            throw new StudyGroupRepositoryException("forbidden", "Điều phối viên không thể quản lý điều phối viên khác.");

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
                break;
            case "promote" when actorIsOwner && target.Status == "Active":
                target.MemberRole = "Moderator";
                break;
            case "demote" when actorIsOwner && target.Status == "Active":
                target.MemberRole = "Member";
                break;
            default:
                throw new StudyGroupRepositoryException("invalid_action", "Thao tác không phù hợp với trạng thái thành viên hiện tại.");
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<GroupInviteData> CreateInviteAsync(
        long groupId,
        long actorUserId,
        int expiresInDays,
        int maxUses,
        CancellationToken cancellationToken = default)
    {
        var group = await context.StudyGroups
            .Include(item => item.StudyGroupSubjects)
                .ThenInclude(subject => subject.Subject)
            .Include(item => item.GroupMembers)
            .SingleOrDefaultAsync(item => item.StudyGroupId == groupId && !item.IsArchived, cancellationToken)
            ?? throw new StudyGroupRepositoryException("not_found", "Không tìm thấy nhóm học tập.");
        var actor = group.GroupMembers.SingleOrDefault(item => item.UserId == actorUserId);
        if (actor?.Status != "Active" || (group.OwnerUserId != actorUserId && actor.MemberRole != "Moderator"))
            throw new StudyGroupRepositoryException("forbidden", "Bạn không có quyền tạo lời mời.");

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
        context.GroupInvites.Add(invite);
        await context.SaveChangesAsync(cancellationToken);
        return ToInvite(invite, group);
    }

    public async Task<GroupInviteData?> GetInviteAsync(
        string inviteCode,
        CancellationToken cancellationToken = default)
    {
        var invite = await context.GroupInvites.AsNoTracking()
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
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var invite = await context.GroupInvites
            .Include(item => item.StudyGroup)
                .ThenInclude(group => group.GroupMembers)
            .SingleOrDefaultAsync(item => item.InviteCode == inviteCode, cancellationToken)
            ?? throw new StudyGroupRepositoryException("invite_not_found", "Lời mời không tồn tại.");
        if (!InviteIsValid(invite) || invite.StudyGroup.IsArchived)
            throw new StudyGroupRepositoryException("invite_expired", "Lời mời đã hết hạn hoặc hết lượt sử dụng.");

        var group = invite.StudyGroup;
        var membership = group.GroupMembers.SingleOrDefault(item => item.UserId == userId);
        if (membership?.Status == "Banned")
            throw new StudyGroupRepositoryException("banned", "Bạn đã bị cấm tham gia nhóm này.");
        if (membership?.Status == "Active")
            return new GroupMembershipData(group.StudyGroupId.ToString(), "Active", "Bạn đã là thành viên của nhóm.");

        EnsureCapacity(group);
        var now = DateTime.UtcNow;
        if (membership is null)
        {
            context.GroupMembers.Add(new GroupMember
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
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GroupMembershipData(group.StudyGroupId.ToString(), "Active", "Đã tham gia nhóm bằng lời mời.");
    }

    public Task<bool> IsActiveMemberAsync(
        long groupId,
        long userId,
        CancellationToken cancellationToken = default) =>
        context.GroupMembers.AsNoTracking().AnyAsync(
            item => item.StudyGroupId == groupId && item.UserId == userId && item.Status == "Active",
            cancellationToken);

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
            ContactUrl = group.ContactUrl ?? string.Empty,
            Rules = group.Rules ?? string.Empty,
            IsPublic = group.Visibility == "Public",
            JoinMode = group.JoinMode,
            MaxMembers = group.MaxMembers,
            OwnerUserId = group.OwnerUserId.ToString(),
            CreatedAt = AsUtc(group.CreatedAtUtc),
            CurrentMembershipStatus = membership?.Status,
            CurrentMemberRole = membership?.MemberRole
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
            .Where(member => member.Status is "Active" or "Pending")
            .OrderBy(member => member.Status == "Pending")
            .ThenBy(member => member.User.DisplayName)
            .Select(member => new GroupMemberData
            {
                UserId = member.UserId.ToString(),
                DisplayName = member.User.DisplayName,
                AvatarUrl = member.User.AvatarUrl,
                Role = group.OwnerUserId == member.UserId ? "Owner" : member.MemberRole,
                Status = member.Status,
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
        if (group.GroupMembers.Count(member => member.Status == "Active") >= group.MaxMembers)
            throw new StudyGroupRepositoryException("group_full", "Nhóm đã đủ số thành viên tối đa.");
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
