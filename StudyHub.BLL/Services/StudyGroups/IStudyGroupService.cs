using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.StudyGroups;

public interface IStudyGroupService
{
    IReadOnlyList<GroupSummaryResponse> GetGroups(string? searchText = null);
    GroupDetailsResponse? GetGroup(string groupId);
    GroupDetailsResponse? Join(string groupId);
    GroupDetailsResponse? Leave(string groupId);
    GroupChatMessageResponse? AddMessage(string groupId, string content);
    GroupDetailsResponse CreateGroup(CreateGroupRequest request);
}
