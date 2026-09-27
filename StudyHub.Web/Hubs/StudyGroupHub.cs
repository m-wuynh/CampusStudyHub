using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StudyHub.BLL.Services.StudyGroups;

namespace StudyHub.Web.Hubs;

[Authorize]
public sealed class StudyGroupHub(IStudyGroupService studyGroups) : Hub
{
    public async Task EnterGroup(string groupId)
    {
        if (!await studyGroups.CanAccessChatAsync(groupId, Context.ConnectionAborted))
            throw new HubException("Bạn phải là thành viên đang hoạt động để mở chat.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId), Context.ConnectionAborted);
    }

    public async Task SendMessage(string groupId, string content)
    {
        try
        {
            var message = await studyGroups.AddMessageAsync(groupId, content, Context.ConnectionAborted);
            await Clients.Group(GroupName(groupId))
                .SendAsync("MessageReceived", message, Context.ConnectionAborted);
        }
        catch (StudyGroupException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    private static string GroupName(string groupId) => $"study-group:{groupId}";
}
