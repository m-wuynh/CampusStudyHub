using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudyHub.BLL.Contracts;
using StudyHub.BLL.Services;

namespace StudyHub.Web.Pages;

public sealed class GroupDetailsModel(IStudyGroupService studyGroups) : PageModel
{
    public GroupDetailsResponse Group { get; private set; } = null!;

    public IActionResult OnGet(string groupId)
    {
        var group = studyGroups.GetGroup(groupId);
        if (group is null)
        {
            return NotFound();
        }

        if (!group.IsMember)
        {
            TempData["GroupError"] = "Bạn cần tham gia nhóm trước khi xem chi tiết.";
            return RedirectToPage("/Groups");
        }

        Group = group;
        return Page();
    }
}
