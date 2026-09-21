using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.StudyGroups.Models;
using DalGroupMember = StudyHub.DAL.Entities.GroupMember;
using DalGroupPost = StudyHub.DAL.Entities.GroupPost;
using DalStudyGroup = StudyHub.DAL.Entities.StudyGroup;
using DalSubject = StudyHub.DAL.Entities.Subject;

namespace StudyHub.DAL.Repositories.StudyGroups;

public sealed class SqlStudyGroupRepository(StudyHubDbContext context) : IStudyGroupRepository
{
    public IReadOnlyList<StudyGroupData> GetAll()
    {
        return context.StudyGroups
            .AsNoTracking()
            .Where(group => !group.IsArchived)
            .Include(group => group.Subject)
            .Include(group => group.GroupMembers)
            .OrderBy(group => group.GroupName)
            .AsEnumerable()
            .Select(ToDomain)
            .ToList();
    }

    public StudyGroupData? GetById(string groupId)
    {
        if (!long.TryParse(groupId, out var databaseId))
        {
            return null;
        }

        var group = context.StudyGroups
            .AsNoTracking()
            .AsSplitQuery()
            .Where(item => !item.IsArchived)
            .Include(item => item.Subject)
            .Include(item => item.GroupMembers)
                .ThenInclude(member => member.User)
            .Include(item => item.GroupMembers)
                .ThenInclude(member => member.GroupPosts)
            .SingleOrDefault(item => item.StudyGroupId == databaseId);

        return group is null ? null : ToDomain(group);
    }

    public StudyGroupData Add(StudyGroupData group)
    {
        if (!long.TryParse(group.OwnerUserId, out var ownerUserId))
        {
            throw new InvalidOperationException("Mã người tạo nhóm không hợp lệ.");
        }

        using var transaction = context.Database.BeginTransaction();
        var subject = context.Subjects.FirstOrDefault(item => item.SubjectName == group.Subject);
        if (subject is null && !string.IsNullOrWhiteSpace(group.Subject))
        {
            subject = new DalSubject
            {
                SubjectCode = $"USR-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                SubjectName = group.Subject.Trim(),
                IsActive = true
            };
            context.Subjects.Add(subject);
        }

        var entity = new DalStudyGroup
        {
            OwnerUserId = ownerUserId,
            Subject = subject,
            GroupName = group.Name,
            Description = group.Description,
            Visibility = group.IsPublic ? "Public" : "Private",
            MaxMembers = 20,
            IsArchived = false,
            CreatedAtUtc = group.CreatedAt.UtcDateTime
        };
        context.StudyGroups.Add(entity);
        context.SaveChanges();

        context.GroupMembers.Add(new DalGroupMember
        {
            StudyGroupId = entity.StudyGroupId,
            UserId = ownerUserId,
            MemberRole = "Moderator",
            Status = "Active",
            RequestedAtUtc = DateTime.UtcNow,
            JoinedAtUtc = DateTime.UtcNow
        });
        context.SaveChanges();
        transaction.Commit();

        return GetById(entity.StudyGroupId.ToString())
            ?? throw new InvalidOperationException("Không thể đọc lại nhóm vừa tạo.");
    }

    public void Update(StudyGroupData group)
    {
        if (!long.TryParse(group.Id, out var databaseId))
        {
            throw new InvalidOperationException("Mã nhóm không hợp lệ.");
        }

        using var transaction = context.Database.BeginTransaction();
        var entity = context.StudyGroups
            .AsSplitQuery()
            .Include(item => item.GroupMembers)
                .ThenInclude(member => member.GroupPosts)
            .SingleOrDefault(item => item.StudyGroupId == databaseId)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhóm có mã '{group.Id}'.");

        if (entity.GroupName != group.Name) entity.GroupName = group.Name;
        if ((entity.Description ?? string.Empty) != group.Description)
            entity.Description = string.IsNullOrWhiteSpace(group.Description) ? null : group.Description;
        var visibility = group.IsPublic ? "Public" : "Private";
        if (entity.Visibility != visibility) entity.Visibility = visibility;

        var activeUserIds = group.MemberUserIds
            .Select(value => long.TryParse(value, out var id) ? id : (long?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToHashSet();

        foreach (var member in entity.GroupMembers)
        {
            if (activeUserIds.Contains(member.UserId))
            {
                member.Status = "Active";
                member.JoinedAtUtc ??= DateTime.UtcNow;
            }
            else if (member.Status == "Active")
            {
                member.Status = "Left";
            }
        }

        var knownUserIds = entity.GroupMembers.Select(member => member.UserId).ToHashSet();
        foreach (var userId in activeUserIds.Except(knownUserIds))
        {
            entity.GroupMembers.Add(new DalGroupMember
            {
                StudyGroupId = entity.StudyGroupId,
                UserId = userId,
                MemberRole = "Member",
                Status = "Active",
                RequestedAtUtc = DateTime.UtcNow,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        var existingPostIds = entity.GroupMembers
            .SelectMany(member => member.GroupPosts)
            .Select(post => post.GroupPostId)
            .ToHashSet();
        foreach (var message in group.Messages)
        {
            if (long.TryParse(message.Id, out var postId) && existingPostIds.Contains(postId))
            {
                continue;
            }

            if (!long.TryParse(message.UserId, out var authorUserId))
            {
                continue;
            }

            context.GroupPosts.Add(new DalGroupPost
            {
                StudyGroupId = entity.StudyGroupId,
                AuthorUserId = authorUserId,
                Body = message.Content,
                IsPinned = false,
                IsDeleted = false,
                CreatedAtUtc = message.SentAt.UtcDateTime,
                UpdatedAtUtc = message.SentAt.UtcDateTime
            });
        }

        context.SaveChanges();
        transaction.Commit();
    }

    private static StudyGroupData ToDomain(DalStudyGroup group)
    {
        var activeMembers = group.GroupMembers
            .Where(member => member.Status == "Active")
            .ToList();
        var messages = group.GroupMembers
            .SelectMany(member => member.GroupPosts
                .Where(post => !post.IsDeleted)
                .Select(post => new GroupChatMessageData
                {
                    Id = post.GroupPostId.ToString(),
                    UserId = member.UserId.ToString(),
                    AuthorName = member.User?.DisplayName ?? $"Thành viên {member.UserId}",
                    Content = post.Body,
                    SentAt = AsUtc(post.CreatedAtUtc)
                }))
            .OrderBy(message => message.SentAt)
            .ToList();

        var subjectName = group.Subject?.SubjectName ?? "Học tập";
        return new StudyGroupData
        {
            Id = group.StudyGroupId.ToString(),
            Name = group.GroupName,
            Subject = subjectName,
            SubjectCssClass = GetSubjectCssClass(subjectName),
            Description = group.Description ?? string.Empty,
            IsPublic = group.Visibility == "Public",
            OwnerUserId = group.OwnerUserId.ToString(),
            CreatedAt = AsUtc(group.CreatedAtUtc),
            MemberUserIds = activeMembers.Select(member => member.UserId.ToString()).ToHashSet(),
            Messages = messages
        };
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static string GetSubjectCssClass(string subject) => subject.Trim().ToLowerInvariant() switch
    {
        "vật lý" => "subject-vatly",
        "hóa học" => "subject-hoahoc",
        "lịch sử" => "subject-lichsu",
        "sinh học" => "subject-sinhhoc",
        "tiếng anh" => "subject-tienganh",
        _ => "subject-toan"
    };
}
