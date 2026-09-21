using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.Web.ViewModels.StudyGroups;

namespace StudyHub.Web.Controllers.StudyGroups;

[Route("Groups")]
public sealed class StudyGroupsController(IStudyGroupService studyGroups) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View(new StudyGroupsIndexViewModel());

    [HttpGet("{groupId}")]
    public IActionResult Details(string groupId)
    {
        var group = studyGroups.GetGroup(groupId);
        if (group is null) return NotFound();

        if (!group.IsMember)
        {
            TempData["GroupError"] = "Bạn cần tham gia nhóm trước khi xem chi tiết.";
            return RedirectToAction(nameof(Index));
        }

        return View(new StudyGroupDetailsViewModel(group));
    }
}
