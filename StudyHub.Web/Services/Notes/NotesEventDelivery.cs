using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Persistence;
using StudyHub.Web.Hubs;

namespace StudyHub.Web.Services.Notes;

public static class NotesEventDelivery
{
    public static async Task SendToActiveMembersAsync(
        StudyHubDbContext db, IHubContext<NotesHub> hub, long groupId,
        string eventName, JsonElement payload, CancellationToken ct)
    {
        var memberIds = await db.GroupMembers.AsNoTracking()
            .Where(member => member.StudyGroupId == groupId && member.Status == "Active" &&
                member.User.Status == "Active" && !member.StudyGroup.IsArchived)
            .Select(member => member.UserId)
            .ToArrayAsync(ct);
        var userIds = memberIds.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (userIds.Length > 0)
            await hub.Clients.Users(userIds).SendAsync(eventName, payload, ct);
    }
}