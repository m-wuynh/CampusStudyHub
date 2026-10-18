using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.StudyGroups;

namespace StudyHub.Web.Controllers.StudyGroups;

[ApiController, Authorize, AutoValidateAntiforgeryToken]
[Route("api/groups/{groupId:long}/learning")]
public sealed class GroupLearningApiController(GroupLearningService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(long groupId, [FromQuery] DateOnly? week, CancellationToken ct) =>
        Execute(async () => Ok(await service.GetAsync(groupId, week, ct)));
    [HttpPost("sessions")]
    public Task<IActionResult> CreateSession(long groupId, SaveGroupSessionRequest request, CancellationToken ct) =>
        Change(() => service.SaveSessionAsync(groupId, 0, request, ct));
    [HttpPut("sessions/{id:long}")]
    public Task<IActionResult> UpdateSession(long groupId, long id, SaveGroupSessionRequest request, CancellationToken ct) =>
        Change(() => service.SaveSessionAsync(groupId, id, request, ct));
    [HttpPost("sessions/{id:long}/status")]
    public Task<IActionResult> SessionStatus(long groupId, long id, ChangeGroupSessionRequest request, CancellationToken ct) =>
        Change(() => service.ChangeSessionAsync(groupId, id, request, ct));
    [HttpPut("sessions/{id:long}/registration")]
    public Task<IActionResult> Register(long groupId, long id, RegisterGroupSessionRequest request, CancellationToken ct) =>
        Change(() => service.RegisterAsync(groupId, id, request.Registered, ct));
    [HttpPut("sessions/{id:long}/attendance/{userId:long}")]
    public Task<IActionResult> Attendance(long groupId, long id, long userId, MarkGroupAttendanceRequest request, CancellationToken ct) =>
        Change(() => service.MarkAttendanceAsync(groupId, id, userId, request.Attended, ct));
    [HttpPost("assignments")]
    public Task<IActionResult> CreateAssignment(long groupId, SaveGroupAssignmentRequest request, CancellationToken ct) =>
        Change(() => service.SaveAssignmentAsync(groupId, 0, request, ct));
    [HttpPut("assignments/{id:long}")]
    public Task<IActionResult> UpdateAssignment(long groupId, long id, SaveGroupAssignmentRequest request, CancellationToken ct) =>
        Change(() => service.SaveAssignmentAsync(groupId, id, request, ct));
    [HttpPost("assignments/{id:long}/close")]
    public Task<IActionResult> Close(long groupId, long id, RevisionRequest request, CancellationToken ct) =>
        Change(() => service.CloseAssignmentAsync(groupId, id, request.Revision, ct));
    [HttpPost("assignments/{id:long}/submissions")]
    public Task<IActionResult> Submit(long groupId, long id, SubmitGroupAssignmentRequest request, CancellationToken ct) =>
        Change(() => service.SubmitAsync(groupId, id, request, ct));
    [HttpPut("assignments/{id:long}/submissions/{submissionId:long}/review")]
    public Task<IActionResult> Review(long groupId, long id, long submissionId, ReviewGroupSubmissionRequest request, CancellationToken ct) =>
        Change(() => service.ReviewAsync(groupId, id, submissionId, request, ct));
    [HttpPut("resources/{kind}/{id:long}")]
    public Task<IActionResult> Share(long groupId, string kind, long id, CancellationToken ct) =>
        Change(() => service.ShareAsync(groupId, kind, id, true, ct));
    [HttpDelete("resources/{kind}/{id:long}")]
    public Task<IActionResult> Unshare(long groupId, string kind, long id, CancellationToken ct) =>
        Change(() => service.ShareAsync(groupId, kind, id, false, ct));
    [HttpPut("goal")]
    public Task<IActionResult> Goal(long groupId, SaveGroupGoalRequest request, CancellationToken ct) =>
        Change(() => service.SaveGoalAsync(groupId, request, ct));
    [HttpPost("questions")]
    public Task<IActionResult> Ask(long groupId, AskGroupQuestionRequest request, CancellationToken ct) =>
        Change(() => service.AskAsync(groupId, request, ct));
    [HttpPost("questions/{id:long}/answers")]
    public Task<IActionResult> Answer(long groupId, long id, AnswerGroupQuestionRequest request, CancellationToken ct) =>
        Change(() => service.AnswerAsync(groupId, id, request, ct));
    [HttpPut("questions/{id:long}/accepted-answer")]
    public Task<IActionResult> Accept(long groupId, long id, AcceptGroupAnswerRequest request, CancellationToken ct) =>
        Change(() => service.AcceptAnswerAsync(groupId, id, request, ct));

    private Task<IActionResult> Change(Func<Task> action) => Execute(async () =>
    {
        await action();
        return Ok(new { message = "Đã lưu thay đổi." });
    });
    private static async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (StudyGroupException e) { return new ObjectResult(new { code = e.Code, message = e.Message }) { StatusCode = e.StatusCode }; }
    }
}
