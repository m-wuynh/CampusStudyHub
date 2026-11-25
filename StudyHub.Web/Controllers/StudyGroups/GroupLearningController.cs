using StudyHub.BLL.DTOs.StudyGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.StudyGroups;

namespace StudyHub.Web.Controllers.StudyGroups;

[Authorize, AutoValidateAntiforgeryToken]
[Route("Groups/{groupId:long}/Learning/[action]")]
public sealed class GroupLearningController(IGroupLearningService service) : Controller
{
    [HttpPost]
    public Task<IActionResult> CreateSession(long groupId, [FromForm] CreateGroupSessionRequest request, CancellationToken ct) =>
        Change(groupId, () => service.CreateSessionAsync(groupId, request, ct));
    [HttpPost]
    public Task<IActionResult> SessionStatus(long groupId, [FromForm, BindRequired] long id, [FromForm] ChangeGroupSessionRequest request, CancellationToken ct) =>
        Change(groupId, () => service.ChangeSessionAsync(groupId, id, request, ct));
    [HttpPost]
    public Task<IActionResult> Register(long groupId, [FromForm, BindRequired] long id, [FromForm] RegisterGroupSessionRequest request, CancellationToken ct) =>
        Change(groupId, () => service.RegisterAsync(groupId, id, request.Registered, ct));
    [HttpPost]
    public Task<IActionResult> Attendance(long groupId, [FromForm, BindRequired] long id, [FromForm] MarkGroupAttendanceRequest request, CancellationToken ct) =>
        Change(groupId, () => service.MarkAttendanceAsync(groupId, id, request, ct));
    [HttpPost]
    public Task<IActionResult> CreateAssignment(long groupId, [FromForm] SaveGroupAssignmentRequest request, CancellationToken ct) =>
        Change(groupId, () => service.SaveAssignmentAsync(groupId, 0, request, ct));
    [HttpPost]
    public Task<IActionResult> UpdateAssignment(long groupId, [FromForm, BindRequired] long id, [FromForm] SaveGroupAssignmentRequest request, CancellationToken ct) =>
        Change(groupId, () => service.SaveAssignmentAsync(groupId, id, request, ct));
    [HttpPost]
    public Task<IActionResult> Close(long groupId, [FromForm, BindRequired] long id, [FromForm] RevisionRequest request, CancellationToken ct) =>
        Change(groupId, () => service.CloseAssignmentAsync(groupId, id, request.Revision, ct));
    [HttpPost]
    public Task<IActionResult> Submit(long groupId, [FromForm, BindRequired] long id, [FromForm] SubmitGroupAssignmentRequest request, CancellationToken ct) =>
        Change(groupId, () => service.SubmitAsync(groupId, id, request, ct));
    [HttpPost]
    public Task<IActionResult> Review(long groupId, [FromForm, BindRequired] long id, [FromForm, BindRequired] long submissionId, [FromForm] ReviewGroupSubmissionRequest request, CancellationToken ct) =>
        Change(groupId, () => service.ReviewAsync(groupId, id, submissionId, request, ct));
    [HttpPost]
    public Task<IActionResult> Share(long groupId, [FromForm, BindRequired] string kind, [FromForm, BindRequired] long id, CancellationToken ct) =>
        Change(groupId, () => service.ShareAsync(groupId, kind, id, true, ct));
    [HttpPost]
    public Task<IActionResult> Unshare(long groupId, [FromForm, BindRequired] string kind, [FromForm, BindRequired] long id, CancellationToken ct) =>
        Change(groupId, () => service.ShareAsync(groupId, kind, id, false, ct));
    [HttpPost]
    public Task<IActionResult> Ask(long groupId, [FromForm] AskGroupQuestionRequest request, CancellationToken ct) =>
        Change(groupId, () => service.AskAsync(groupId, request, ct));
    [HttpPost]
    public Task<IActionResult> Answer(long groupId, [FromForm, BindRequired] long id, [FromForm] AnswerGroupQuestionRequest request, CancellationToken ct) =>
        Change(groupId, () => service.AnswerAsync(groupId, id, request, ct));
    [HttpPost]
    public Task<IActionResult> Accept(long groupId, [FromForm, BindRequired] long id, [FromForm] AcceptGroupAnswerRequest request, CancellationToken ct) =>
        Change(groupId, () => service.AcceptAnswerAsync(groupId, id, request, ct));

    private async Task<IActionResult> Change(long groupId, Func<Task> operation)
    {
        var panel = Request.Form["panel"].ToString();
        if (panel is not ("sessions" or "assignments" or "questions" or "resources" or "progress"))
            panel = "sessions";
        var week = DateOnly.TryParse(Request.Form["week"], out var selectedWeek)
            ? selectedWeek.ToString("yyyy-MM-dd") : null;
        if (!ModelState.IsValid)
            TempData["GroupError"] = "Dữ liệu không hợp lệ. Hãy kiểm tra và thử lại.";
        else
        {
            try
            {
                await operation();
                TempData["GroupSuccess"] = "Đã lưu thay đổi.";
            }
            catch (StudyGroupException exception)
            {
                if (exception.StatusCode == 403) return Forbid();
                if (exception.StatusCode == 404) return NotFound();
                TempData["GroupError"] = exception.Message;
            }
        }
        return RedirectToAction(nameof(StudyGroupsController.Details), "StudyGroups", new { groupId, panel, week });
    }
}
