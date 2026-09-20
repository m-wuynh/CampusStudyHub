using System.Text.Json;
using StudyHub.Business.Entities;
using StudyHub.Business.Repositories;
using StudyHub.Business.Services;

namespace StudyHub.Data.Repositories;

public sealed class JsonStudyGroupRepository : IStudyGroupRepository
{
    private readonly object _sync = new();
    private readonly string _dataFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private List<StudyGroup> _groups;

    public JsonStudyGroupRepository(string dataFilePath)
    {
        _dataFilePath = dataFilePath;
        var dataDirectory = Path.GetDirectoryName(_dataFilePath)
            ?? throw new ArgumentException("Đường dẫn dữ liệu không hợp lệ.", nameof(dataFilePath));
        Directory.CreateDirectory(dataDirectory);
        _groups = Load();

        if (_groups.Count == 0)
        {
            _groups = CreateSeedGroups();
            SaveUnsafe();
        }
    }

    public IReadOnlyList<StudyGroup> GetAll()
    {
        lock (_sync)
        {
            return _groups.Select(Clone).ToList();
        }
    }

    public StudyGroup? GetById(string groupId)
    {
        lock (_sync)
        {
            var group = _groups.FirstOrDefault(item =>
                item.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase));
            return group is null ? null : Clone(group);
        }
    }

    public void Add(StudyGroup group)
    {
        lock (_sync)
        {
            if (_groups.Any(item => item.Id.Equals(group.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Nhóm có mã '{group.Id}' đã tồn tại.");
            }

            _groups.Add(Clone(group));
            SaveUnsafe();
        }
    }

    public void Update(StudyGroup group)
    {
        lock (_sync)
        {
            var index = _groups.FindIndex(item =>
                item.Id.Equals(group.Id, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new KeyNotFoundException($"Không tìm thấy nhóm có mã '{group.Id}'.");
            }

            _groups[index] = Clone(group);
            SaveUnsafe();
        }
    }

    private List<StudyGroup> Load()
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
            var backupPath = $"{_dataFilePath}.invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            File.Copy(_dataFilePath, backupPath, true);
            return [];
        }
    }

    private void SaveUnsafe()
    {
        var temporaryPath = $"{_dataFilePath}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_groups, _jsonOptions));
        File.Move(temporaryPath, _dataFilePath, true);
    }

    private static StudyGroup Clone(StudyGroup group) => new()
    {
        Id = group.Id,
        Name = group.Name,
        Subject = group.Subject,
        SubjectCssClass = group.SubjectCssClass,
        Description = group.Description,
        IsPublic = group.IsPublic,
        OwnerUserId = group.OwnerUserId,
        CreatedAt = group.CreatedAt,
        MemberUserIds = [.. group.MemberUserIds],
        Messages = group.Messages.Select(message => new GroupChatMessage
        {
            Id = message.Id,
            UserId = message.UserId,
            AuthorName = message.AuthorName,
            Content = message.Content,
            SentAt = message.SentAt
        }).ToList()
    };

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
                UserId = StudyGroupService.CurrentUserId,
                AuthorName = StudyGroupService.CurrentUserName,
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
            members.Add(StudyGroupService.CurrentUserId);
        }

        return new StudyGroup
        {
            Id = id,
            Name = name,
            Subject = subject,
            SubjectCssClass = subjectCssClass,
            Description = description,
            IsPublic = true,
            OwnerUserId = currentUserIsOwner ? StudyGroupService.CurrentUserId : $"owner-{id}",
            MemberUserIds = members,
            Messages = messages ?? []
        };
    }
}
