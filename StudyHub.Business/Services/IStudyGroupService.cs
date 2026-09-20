using StudyHub.Business.Contracts;

namespace StudyHub.Business.Services;

public interface IStudyGroupService
{
    IReadOnlyList<GroupSummaryResponse> GetGroups();
    GroupDetailsResponse? GetGroup(string groupId);
    GroupDetailsResponse? Join(string groupId);
    GroupDetailsResponse? Leave(string groupId);
    GroupChatMessageResponse? AddMessage(string groupId, string content);
    GroupDetailsResponse CreateGroup(CreateGroupRequest request);
}
