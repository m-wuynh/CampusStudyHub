using StudyHub.BLL.Contracts;

namespace StudyHub.BLL.Services;

public interface IStudyGroupService
{
    IReadOnlyList<GroupSummaryResponse> GetGroups();
    GroupDetailsResponse? GetGroup(string groupId);
    GroupDetailsResponse? Join(string groupId);
    GroupDetailsResponse? Leave(string groupId);
    GroupChatMessageResponse? AddMessage(string groupId, string content);
    GroupDetailsResponse CreateGroup(CreateGroupRequest request);
}
