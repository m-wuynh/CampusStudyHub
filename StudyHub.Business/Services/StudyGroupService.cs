using StudyHub.Business.Contracts;
using StudyHub.Business.Entities;
using StudyHub.Business.Repositories;

namespace StudyHub.Business.Services;

public sealed class StudyGroupService(IStudyGroupRepository repository) : IStudyGroupService
{
    public const string CurrentUserId = "demo-user";
    public const string CurrentUserName = "Nguyễn Minh Anh";

    private readonly object _sync = new();

    public IReadOnlyList<GroupSummaryResponse> GetGroups()
    {
        return repository.GetAll()
            .OrderByDescending(group => group.MemberUserIds.Contains(CurrentUserId))
            .ThenBy(group => group.Name)
            .Select(ToSummary)
            .ToList();
    }

    public GroupDetailsResponse? GetGroup(string groupId)
    {
        var group = repository.GetById(groupId);
        return group is null ? null : ToDetails(group);
    }

    public GroupDetailsResponse? Join(string groupId)
    {
        lock (_sync)
        {
            var group = repository.GetById(groupId);
            if (group is null)
            {
                return null;
            }

            if (group.MemberUserIds.Add(CurrentUserId))
            {
                repository.Update(group);
            }

            return ToDetails(group);
        }
    }

    public GroupDetailsResponse? Leave(string groupId)
    {
        lock (_sync)
        {
            var group = repository.GetById(groupId);
            if (group is null)
            {
                return null;
            }

            if (group.OwnerUserId == CurrentUserId)
            {
                throw new InvalidOperationException("Trưởng nhóm không thể rời nhóm của mình.");
            }

            if (group.MemberUserIds.Remove(CurrentUserId))
            {
                repository.Update(group);
            }

            return ToDetails(group);
        }
    }

    public GroupChatMessageResponse? AddMessage(string groupId, string content)
    {
        lock (_sync)
        {
            var group = repository.GetById(groupId);
            if (group is null || !group.MemberUserIds.Contains(CurrentUserId))
            {
                return null;
            }

            var message = new GroupChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = CurrentUserId,
                AuthorName = CurrentUserName,
                Content = content,
                SentAt = DateTimeOffset.UtcNow
            };

            group.Messages.Add(message);
            repository.Update(group);
            return ToMessage(message);
        }
    }

    public GroupDetailsResponse CreateGroup(CreateGroupRequest request)
    {
        lock (_sync)
        {
            var existingIds = repository.GetAll()
                .Select(group => group.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var idBase = Slugify(request.Name!);
            var id = idBase;
            var suffix = 2;
            while (existingIds.Contains(id))
            {
                id = $"{idBase}-{suffix++}";
            }

            var group = new StudyGroup
            {
                Id = id,
                Name = request.Name!.Trim(),
                Subject = request.Subject!.Trim(),
                SubjectCssClass = GetSubjectCssClass(request.Subject),
                Description = request.Description?.Trim() ?? string.Empty,
                IsPublic = request.IsPublic,
                OwnerUserId = CurrentUserId,
                MemberUserIds = [CurrentUserId]
            };

            repository.Add(group);
            return ToDetails(group);
        }
    }

    private static GroupSummaryResponse ToSummary(StudyGroup group) => new(
        group.Id,
        group.Name,
        group.Subject,
        group.SubjectCssClass,
        group.Description,
        group.MemberUserIds.Count,
        group.MemberUserIds.Contains(CurrentUserId),
        group.OwnerUserId == CurrentUserId,
        group.IsPublic);

    private static GroupDetailsResponse ToDetails(StudyGroup group) => new(
        group.Id,
        group.Name,
        group.Subject,
        group.SubjectCssClass,
        group.Description,
        group.MemberUserIds.Count,
        group.MemberUserIds.Contains(CurrentUserId),
        group.OwnerUserId == CurrentUserId,
        group.MemberUserIds.Contains(CurrentUserId)
            ? group.Messages.OrderBy(message => message.SentAt).Select(ToMessage).ToList()
            : []);

    private static GroupChatMessageResponse ToMessage(GroupChatMessage message) => new(
        message.Id,
        message.AuthorName,
        message.Content,
        message.SentAt,
        message.UserId == CurrentUserId);

    private static string GetSubjectCssClass(string? subject) => subject?.Trim().ToLowerInvariant() switch
    {
        "vật lý" => "subject-vatly",
        "hóa học" => "subject-hoahoc",
        "lịch sử" => "subject-lichsu",
        "sinh học" => "subject-sinhhoc",
        "tiếng anh" => "subject-tienganh",
        _ => "subject-toan"
    };

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace('đ', 'd')
            .Normalize(System.Text.NormalizationForm.FormD);
        var characters = normalized
            .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var slug = string.Join('-', new string(characters).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? $"group-{Guid.NewGuid():N}" : slug;
    }
}
