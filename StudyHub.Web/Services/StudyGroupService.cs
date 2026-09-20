using System.Text.Json;
using StudyHub.Web.Models;

namespace StudyHub.Web.Services;

public sealed class StudyGroupService
{
    public const string CurrentUserId = "demo-user";
    public const string CurrentUserName = "Nguyễn Minh Anh";

    private readonly object _sync = new();
    private readonly string _dataFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private List<StudyGroup> _groups;

    public StudyGroupService(IWebHostEnvironment environment, ILogger<StudyGroupService> logger)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _dataFilePath = Path.Combine(dataDirectory, "study-groups.json");
        _groups = LoadGroups(logger);

        if (_groups.Count == 0)
        {
            _groups = CreateSeedGroups();
            SaveUnsafe();
        }
    }

    public IReadOnlyList<GroupSummaryResponse> GetGroups()
    {
        lock (_sync)
        {
            return _groups
                .OrderByDescending(group => group.MemberUserIds.Contains(CurrentUserId))
                .ThenBy(group => group.Name)
                .Select(ToSummary)
                .ToList();
        }
    }

    public GroupDetailsResponse? GetGroup(string groupId)
    {
        lock (_sync)
        {
            var group = FindGroupUnsafe(groupId);
            return group is null ? null : ToDetails(group);
        }
    }

    public GroupDetailsResponse? Join(string groupId)
    {
        lock (_sync)
        {
            var group = FindGroupUnsafe(groupId);
            if (group is null)
            {
                return null;
            }

            if (group.MemberUserIds.Add(CurrentUserId))
            {
                SaveUnsafe();
            }

            return ToDetails(group);
        }
    }

    public GroupDetailsResponse? Leave(string groupId)
    {
        lock (_sync)
        {
            var group = FindGroupUnsafe(groupId);
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
                SaveUnsafe();
            }

            return ToDetails(group);
        }
    }

    public GroupChatMessageResponse? AddMessage(string groupId, string content)
    {
        lock (_sync)
        {
            var group = FindGroupUnsafe(groupId);
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
            SaveUnsafe();
            return ToMessage(message);
        }
    }

    public GroupDetailsResponse CreateGroup(CreateGroupRequest request)
    {
        lock (_sync)
        {
            var idBase = Slugify(request.Name!);
            var id = idBase;
            var suffix = 2;
            while (_groups.Any(group => group.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
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

            _groups.Add(group);
            SaveUnsafe();
            return ToDetails(group);
        }
    }

    private List<StudyGroup> LoadGroups(ILogger logger)
    {
        if (!File.Exists(_dataFilePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_dataFilePath);
            return JsonSerializer.Deserialize<List<StudyGroup>>(json, _jsonOptions) ?? [];
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            logger.LogWarning(exception, "Không thể đọc dữ liệu nhóm học. Dữ liệu mẫu sẽ được tạo lại.");
            return [];
        }
    }

    private void SaveUnsafe()
    {
        var temporaryPath = $"{_dataFilePath}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_groups, _jsonOptions));
        File.Move(temporaryPath, _dataFilePath, true);
    }

    private StudyGroup? FindGroupUnsafe(string groupId) =>
        _groups.FirstOrDefault(group => group.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase));

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

    private static List<StudyGroup> CreateSeedGroups()
    {
        var demoMessages = new List<GroupChatMessage>
        {
            new()
            {
                Id = "demo-message-1",
                UserId = "tran-van-binh",
                AuthorName = "Trần Văn Bình",
                Content = "Mọi người đã làm xong đề số 1 chưa?",
                SentAt = DateTimeOffset.UtcNow.AddMinutes(-15)
            },
            new()
            {
                Id = "demo-message-2",
                UserId = CurrentUserId,
                AuthorName = CurrentUserName,
                Content = "Mình làm xong phần đầu rồi, tối nay mình gửi lời giải nhé!",
                SentAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            }
        };

        return
        [
            CreateSeedGroup("math-advanced", "Toán Chuyên sâu 11A1", "Toán", "subject-toan", "Ôn luyện và giải đề thi thử giữa kỳ cho lớp 11A1", 8, true, true, demoMessages),
            CreateSeedGroup("chemistry-organic", "Hóa học hữu cơ – Nhóm ôn thi", "Hóa học", "subject-hoahoc", "Ôn tập Hóa hữu cơ, giải bài tập theo chủ đề", 12, true),
            CreateSeedGroup("ielts-prep", "Tiếng Anh – IELTS Prep Group", "Tiếng Anh", "subject-tienganh", "Luyện 4 kỹ năng và chia sẻ tài liệu IELTS", 20, true),
            CreateSeedGroup("physics-mechanics", "Vật lý Cơ học – Nhóm luyện đề", "Vật lý", "subject-vatly", "Tập trung giải đề thi cơ học, điện học và quang học.", 46),
            CreateSeedGroup("vietnam-history", "Lịch sử Việt Nam – Ôn thi THPTQG", "Lịch sử", "subject-lichsu", "Ôn tập lịch sử theo chủ đề, giai đoạn và sự kiện quan trọng.", 15),
            CreateSeedGroup("biology-genetics", "Sinh học – Di truyền và Tiến hóa", "Sinh học", "subject-sinhhoc", "Chuyên sâu về di truyền học, gen và cơ chế tiến hóa.", 22)
        ];
    }

    private static StudyGroup CreateSeedGroup(
        string id,
        string name,
        string subject,
        string subjectCssClass,
        string description,
        int memberCount,
        bool currentUserIsMember = false,
        bool currentUserIsOwner = false,
        List<GroupChatMessage>? messages = null)
    {
        var members = Enumerable.Range(1, Math.Max(memberCount - (currentUserIsMember ? 1 : 0), 0))
            .Select(index => $"seed-member-{id}-{index}")
            .ToHashSet();
        if (currentUserIsMember)
        {
            members.Add(CurrentUserId);
        }

        return new StudyGroup
        {
            Id = id,
            Name = name,
            Subject = subject,
            SubjectCssClass = subjectCssClass,
            Description = description,
            IsPublic = true,
            OwnerUserId = currentUserIsOwner ? CurrentUserId : $"owner-{id}",
            MemberUserIds = members,
            Messages = messages ?? []
        };
    }
}
