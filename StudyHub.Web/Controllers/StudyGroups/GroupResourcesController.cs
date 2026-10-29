using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.Web.ViewModels.StudyGroups;

namespace StudyHub.Web.Controllers.StudyGroups;

[Authorize]
[Route("Groups/{groupId:long}/Resources")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class GroupResourcesController(
    GroupLearningService service) : Controller
{
    [HttpGet("{kind}/{id:long}")]
    public async Task<IActionResult> Read(
        long groupId,
        string kind,
        long id,
        CancellationToken ct)
    {
        // Notes/PDF mới đi qua NotesV3Controller.
        if (kind != "Flashcard")
        {
            return NotFound();
        }

        try
        {
            var content = await service.ReadResourceAsync(
                groupId,
                kind,
                id,
                ct);

            return View(
                "~/Views/StudyGroups/Resource.cshtml",
                new GroupResourceViewModel(
                    groupId,
                    id,
                    content,
                    new List<SharedNotePage>()));
        }
        catch (StudyGroupException exception)
        {
            return StatusCode(
                exception.StatusCode,
                exception.Message);
        }
    }
}