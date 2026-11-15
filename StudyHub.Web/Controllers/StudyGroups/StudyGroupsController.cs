using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.Web.Hubs;
using StudyHub.Web.ViewModels.StudyGroups;

namespace StudyHub.Web.Controllers.StudyGroups;

[Authorize]
[AutoValidateAntiforgeryToken]
[Route("Groups")]
public sealed class StudyGroupsController(IStudyGroupService studyGroups, IHubContext<StudyGroupHub> hub) : Controller
{
    [AllowAnonymous]
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery(Name = "q")] string? searchText, CancellationToken cancellationToken) =>
        View(new StudyGroupsIndexViewModel(User.Identity?.IsAuthenticated == true
            ? await studyGroups.GetGroupsAsync(searchText?.Trim(), cancellationToken)
            : [], searchText?.Trim()));

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", new StudyGroupFormViewModel { SubjectOptions = await studyGroups.GetSubjectOptionsAsync(cancellationToken) });

    [HttpPost("Create")]
    public async Task<IActionResult> Create(StudyGroupFormViewModel model, CancellationToken cancellationToken)
    {
        model.GroupId = null;
        if (ModelState.IsValid)
        {
            try
            {
                var group = await studyGroups.CreateGroupAsync(model.ToCreateRequest(), cancellationToken);
                TempData["GroupSuccess"] = "Đã tạo nhóm học tập.";
                return RedirectToAction(nameof(Details), new { groupId = group.Id });
            }
            catch (StudyGroupException exception) { ModelState.AddModelError("", exception.Message); }
        }
        model.SubjectOptions = await studyGroups.GetSubjectOptionsAsync(cancellationToken);
        return View("Form", model);
    }

    [HttpGet("{groupId:long}/Edit")]
    public async Task<IActionResult> Edit(string groupId, CancellationToken cancellationToken)
    {
        var group = await studyGroups.GetGroupAsync(groupId, cancellationToken);
        if (group is null) return NotFound();
        if (!group.IsOwner) return Forbid();
        return View("Form", new StudyGroupFormViewModel
        {
            GroupId = group.Id, Name = group.Name, Subjects = group.Subjects.Select(s => s.Name).ToList(),
            Description = group.Description, Goal = group.Goal, MeetingFormat = group.MeetingFormat,
            MeetingSchedule = group.MeetingSchedule, Rules = group.Rules, JoinMode = group.JoinMode,
            MaxMembers = (short?)group.MaxMembers, SubjectOptions = await studyGroups.GetSubjectOptionsAsync(cancellationToken)
        });
    }

    [HttpPost("{groupId:long}/Edit")]
    public async Task<IActionResult> Edit(string groupId, StudyGroupFormViewModel model, CancellationToken cancellationToken)
    {
        var group = await studyGroups.GetGroupAsync(groupId, cancellationToken);
        if (group is null) return NotFound();
        if (!group.IsOwner) return Forbid();
        model.GroupId = groupId;
        if (ModelState.IsValid)
        {
            try
            {
                await studyGroups.UpdateGroupAsync(groupId, model.ToUpdateRequest(), cancellationToken);
                TempData["GroupSuccess"] = "Đã cập nhật nhóm học tập.";
                return RedirectToAction(nameof(Details), new { groupId });
            }
            catch (StudyGroupException exception) { ModelState.AddModelError("", exception.Message); }
        }
        model.SubjectOptions = await studyGroups.GetSubjectOptionsAsync(cancellationToken);
        return View("Form", model);
    }

    [HttpPost("{groupId:long}/Delete")]
    public async Task<IActionResult> Delete(string groupId, CancellationToken cancellationToken)
    {
        try
        {
            await studyGroups.DeleteGroupAsync(groupId, cancellationToken);
            TempData["GroupSuccess"] = "Đã xóa nhóm học tập.";
            return RedirectToAction(nameof(Index));
        }
        catch (StudyGroupException exception)
        {
            if (exception.StatusCode == 403) return Forbid();
            if (exception.StatusCode == 404) return NotFound();
            TempData["GroupError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { groupId });
        }
    }

    [HttpPost("{groupId:long}/Join")]
    public Task<IActionResult> Join(string groupId, CancellationToken cancellationToken) =>
        Perform(groupId, async () => (await studyGroups.JoinAsync(groupId, cancellationToken)).Message);

    [HttpPost("{groupId:long}/Leave")]
    public Task<IActionResult> Leave(string groupId, CancellationToken cancellationToken) =>
        Perform(groupId, async () => (await studyGroups.LeaveOrWithdrawAsync(groupId, cancellationToken)).Message, toIndex: true);

    [HttpPost("{groupId:long}/RequestUnban")]
    public Task<IActionResult> RequestUnban(string groupId, CancellationToken cancellationToken) =>
        Perform(groupId, async () => (await studyGroups.RequestUnbanAsync(groupId, cancellationToken)).Message);

    [HttpPost("{groupId:long}/Messages")]
    public Task<IActionResult> AddMessage(string groupId, string? content, CancellationToken cancellationToken) =>
        Perform(groupId, async () =>
        {
            var message = await studyGroups.AddMessageAsync(groupId, content ?? "", cancellationToken);
            await hub.Clients.Group($"study-group:{groupId}").SendAsync("MessageReceived", message, cancellationToken);
            return "Đã gửi tin nhắn.";
        }, "chat");

    [HttpPost("{groupId:long}/Announcements")]
    public Task<IActionResult> AddAnnouncement(string groupId, string? content, bool isPinned, CancellationToken cancellationToken) =>
        Perform(groupId, async () =>
        {
            await studyGroups.AddAnnouncementAsync(groupId, content ?? "", isPinned, cancellationToken);
            return "Đã đăng thông báo.";
        }, "announcements");

    [HttpPost("{groupId:long}/Members/{userId:long}")]
    public Task<IActionResult> ManageMember(string groupId, string userId, string memberAction, CancellationToken cancellationToken) =>
        Perform(groupId, async () =>
        {
            await studyGroups.ManageMemberAsync(groupId, userId, memberAction, cancellationToken);
            return "Đã cập nhật thành viên.";
        }, "members");

    [HttpPost("{groupId:long}/Invites")]
    public Task<IActionResult> CreateInvite(string groupId, CancellationToken cancellationToken) =>
        Perform(groupId, async () =>
        {
            var invite = await studyGroups.CreateInviteAsync(groupId, 7, 20, cancellationToken);
            TempData["GroupInviteUrl"] = Url.Action(nameof(Invite), new { inviteCode = invite.InviteCode });
            TempData["GroupInviteMeta"] = $"Hết hạn {invite.ExpiresAt:dd/MM/yyyy HH:mm} UTC · tối đa {invite.MaxUses} lượt";
            return "Đã tạo lời mời.";
        }, "members");

    private async Task<IActionResult> Perform(string groupId, Func<Task<string>> operation, string? panel = null, bool toIndex = false)
    {
        if (!ModelState.IsValid)
        {
            TempData["GroupError"] = "Dữ liệu không hợp lệ. Hãy kiểm tra và thử lại.";
            return RedirectToAction(nameof(Details), new { groupId, panel });
        }
        try { TempData["GroupSuccess"] = await operation(); }
        catch (StudyGroupException exception)
        {
            TempData["GroupError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { groupId, panel });
        }
        return toIndex ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { groupId, panel });
    }

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
