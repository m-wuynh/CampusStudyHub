using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.Web.ViewModels.StudyGroups;

namespace StudyHub.Web.Controllers.StudyGroups;

[Authorize]
[Route("Groups")]
public sealed class StudyGroupsController(IStudyGroupService studyGroups) : Controller
{
    [HttpGet("")]
    public IActionResult Index([FromQuery(Name = "q")] string? searchText = null) =>
        View(new StudyGroupsIndexViewModel(searchText?.Trim()));

    [HttpGet("{groupId:long}")]
    public async Task<IActionResult> Details(string groupId, CancellationToken cancellationToken)
    {
        var group = await studyGroups.GetGroupAsync(groupId, cancellationToken);
        return group is null ? NotFound() : View(new StudyGroupDetailsViewModel(group));
    }

    [HttpGet("Invite/{inviteCode}")]
    public async Task<IActionResult> Invite(string inviteCode, CancellationToken cancellationToken)
    {
        try
        {
            var invite = await studyGroups.GetInviteAsync(inviteCode, cancellationToken);
            return invite is null ? NotFound() : View(new StudyGroupInviteViewModel(invite));
        }
        catch (StudyGroupException)
        {
            return NotFound();
        }
    }

    [ValidateAntiForgeryToken]
    [HttpPost("Invite/{inviteCode}/Accept")]
    public async Task<IActionResult> AcceptInvite(string inviteCode, CancellationToken cancellationToken)
    {
        try
        {
            var membership = await studyGroups.AcceptInviteAsync(inviteCode, cancellationToken);
            TempData["GroupSuccess"] = membership.Message;
            return RedirectToAction(nameof(Details), new { groupId = membership.GroupId });
        }
        catch (StudyGroupException exception)
        {
            var invite = await studyGroups.GetInviteAsync(inviteCode, cancellationToken);
            return invite is null
                ? NotFound()
                : View("Invite", new StudyGroupInviteViewModel(invite, exception.Message));
        }
    }
}
