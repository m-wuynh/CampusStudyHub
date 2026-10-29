using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.NotesV3.Collaboration;
using StudyHub.Web.Filters;

namespace StudyHub.Web.Controllers.NotesV3;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/notesv3/pages/{pageId:long}/updates")]
[ServiceFilter(typeof(NotesV3ExceptionFilter))]
public sealed class CollaborationApiController(
    ICollaborationService collaboration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(
        long pageId,
        [FromQuery] long after = 0,
        CancellationToken ct = default)
    {
        return Ok(await collaboration.ReadUpdatesAsync(
            pageId,
            after,
            ct));
    }
}