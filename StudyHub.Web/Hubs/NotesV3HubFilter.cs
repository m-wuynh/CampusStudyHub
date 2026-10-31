using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using StudyHub.BLL.Services.NotesV3;

namespace StudyHub.Web.Hubs;

public sealed class NotesV3HubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (NotesV3Exception exception)
        {
            throw new HubException(
                JsonSerializer.Serialize(new
                {
                    status = exception.StatusCode,
                    title = exception.Message
                }));
        }
    }
}