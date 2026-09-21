using StudyHub.BLL.DTOs;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.StudyGroups.Models;

namespace StudyHub.BLL.Services.StudyGroups;

public sealed class StudyGroupService(IRepository repository) : IStudyGroupService
{
    public const string CurrentUserId = "1";
    public const string CurrentUserName = "Nguyễn Minh Anh (demo)";

    private readonly object _sync = new();

    public IReadOnlyList<GroupSummaryResponse> GetGroups()
    {
        return repository.StudyGroups.GetAll()
            .OrderByDescending(group => group.MemberUserIds.Contains(CurrentUserId))
            .ThenBy(group => group.Name)
            .Select(ToSummary)
            .ToList();
    }

    public GroupDetailsResponse? GetGroup(string groupId)
    {
        var group = repository.StudyGroups.GetById(groupId);
        return group is null ? null : ToDetails(group);
    }

    public GroupDetailsResponse? Join(string groupId)
    {
        lock (_sync)
        {
            var group = repository.StudyGroups.GetById(groupId);
            if (group is null) return null;
            if (group.MemberUserIds.Add(CurrentUserId)) repository.StudyGroups.Update(group);
            return ToDetails(group);
        }
    }

    public GroupDetailsResponse? Leave(string groupId)
    {
        lock (_sync)
        {
            var group = repository.StudyGroups.GetById(groupId);
            if (group is null) return null;
            if (group.OwnerUserId == CurrentUserId)
                throw new InvalidOperationException("Trưởng nhóm không thể rời nhóm của mình.");
            if (group.MemberUserIds.Remove(CurrentUserId)) repository.StudyGroups.Update(group);
            return ToDetails(group);
        }
    }

    public GroupChatMessageResponse? AddMessage(string groupId, string content)
    {
        lock (_sync)
        {
            var group = repository.StudyGroups.GetById(groupId);
            if (group is null || !group.MemberUserIds.Contains(CurrentUserId)) return null;

            var message = new GroupChatMessageData
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = CurrentUserId,
                AuthorName = CurrentUserName,
                Content = content,
                SentAt = DateTimeOffset.UtcNow
            };
            group.Messages.Add(message);
            repository.StudyGroups.Update(group);
            return ToMessage(message);
        }
    }

    public GroupDetailsResponse CreateGroup(CreateGroupRequest request)
    {
        lock (_sync)
        {
            var group = new StudyGroupData
            {
                Id = "0",
                Name = request.Name!.Trim(),
                Subject = request.Subject!.Trim(),
                SubjectCssClass = GetSubjectCssClass(request.Subject),
                Description = request.Description?.Trim() ?? string.Empty,
                IsPublic = request.IsPublic,
                OwnerUserId = CurrentUserId,
                MemberUserIds = [CurrentUserId]
            };
            var createdGroup = repository.StudyGroups.Add(group);
            return ToDetails(createdGroup);
        }
    }

    private static GroupSummaryResponse ToSummary(StudyGroupData group) => new(
        group.Id, group.Name, group.Subject, group.SubjectCssClass, group.Description,
        group.MemberUserIds.Count, group.MemberUserIds.Contains(CurrentUserId),
        group.OwnerUserId == CurrentUserId, group.IsPublic);

    private static GroupDetailsResponse ToDetails(StudyGroupData group) => new(
        group.Id, group.Name, group.Subject, group.SubjectCssClass, group.Description,
        group.MemberUserIds.Count, group.MemberUserIds.Contains(CurrentUserId),
        group.OwnerUserId == CurrentUserId,
        group.MemberUserIds.Contains(CurrentUserId)
            ? group.Messages.OrderBy(message => message.SentAt).Select(ToMessage).ToList()
            : []);

    private static GroupChatMessageResponse ToMessage(GroupChatMessageData message) => new(
        message.Id, message.AuthorName, message.Content, message.SentAt,
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
}
