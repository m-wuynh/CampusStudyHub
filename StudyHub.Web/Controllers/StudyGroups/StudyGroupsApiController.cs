using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.StudyGroups;

namespace StudyHub.Web.Controllers.StudyGroups;

[ApiController]
[Authorize]
[Route("api/groups")]
public sealed class StudyGroupsApiController(IStudyGroupService studyGroups) : ControllerBase
{
    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjectOptions(CancellationToken cancellationToken) =>
        Ok(await studyGroups.GetSubjectOptionsAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await studyGroups.GetGroupsAsync(search, cancellationToken));

    [HttpGet("{groupId:long}")]
    public async Task<IActionResult> GetById(string groupId, CancellationToken cancellationToken)
    {
        var group = await studyGroups.GetGroupAsync(groupId, cancellationToken);
        return group is null ? NotFound() : Ok(group);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{groupId:long}/join")]
    public Task<IActionResult> Join(string groupId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await studyGroups.JoinAsync(groupId, cancellationToken)));

    [ValidateAntiForgeryToken]
    [HttpDelete("{groupId:long}/join")]
    public Task<IActionResult> Leave(string groupId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await studyGroups.LeaveOrWithdrawAsync(groupId, cancellationToken)));

    [ValidateAntiForgeryToken]
    [HttpPost("{groupId:long}/messages")]
    public Task<IActionResult> AddMessage(
        string groupId,
        SendGroupMessageRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await studyGroups.AddMessageAsync(
            groupId, request.Content ?? string.Empty, cancellationToken)));

    [ValidateAntiForgeryToken]
    [HttpPost("{groupId:long}/announcements")]
    public Task<IActionResult> AddAnnouncement(
        string groupId,
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await studyGroups.AddAnnouncementAsync(
            groupId, request.Content ?? string.Empty, request.IsPinned, cancellationToken)));

    [ValidateAntiForgeryToken]
    [HttpPost]
    public Task<IActionResult> Create(CreateGroupRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var group = await studyGroups.CreateGroupAsync(request, cancellationToken);
            return Created($"/api/groups/{group.Id}", group);
        });

    [ValidateAntiForgeryToken]
    [HttpPut("{groupId:long}")]
    public Task<IActionResult> Update(
        string groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await studyGroups.UpdateGroupAsync(
            groupId, request, cancellationToken)));

    [ValidateAntiForgeryToken]
    [HttpPost("{groupId:long}/members/{userId:long}")]
    public Task<IActionResult> ManageMember(
        string groupId,
        string userId,
        ManageGroupMemberRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            await studyGroups.ManageMemberAsync(
                groupId, userId, request.Action ?? string.Empty, cancellationToken);
            return Ok(new { message = "Đã cập nhật thành viên." });
        });

    [ValidateAntiForgeryToken]
    [HttpPost("{groupId:long}/invites")]
    public Task<IActionResult> CreateInvite(
        string groupId,
        CreateGroupInviteRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await studyGroups.CreateInviteAsync(
            groupId, request.ExpiresInDays, request.MaxUses, cancellationToken)));

    private static async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (StudyGroupException exception)
        {
            return new ObjectResult(new { code = exception.Code, message = exception.Message })
            {
                StatusCode = exception.StatusCode
            };
        }
    }
}
