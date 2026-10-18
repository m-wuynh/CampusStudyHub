using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Notes;

namespace StudyHub.Web.Hubs;

[Authorize]
public sealed class NotesHub(IStudyGroupGateway groups, ICurrentUser currentUser) : Hub
{
    public static string Room(long groupId) => $"notes-group:{groupId}";

    public async Task EnterGroup(long groupId)
    {
        if (groupId <= 0 || await groups.GetActiveGroupAsync(groupId, Context.ConnectionAborted) is null ||
            !await groups.IsActiveMemberAsync(currentUser.UserId, groupId, Context.ConnectionAborted))
            throw new HubException("Không còn quyền truy cập tài nguyên nhóm.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Room(groupId), Context.ConnectionAborted);
    }
}
