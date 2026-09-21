using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.StudyGroups;

namespace StudyHub.Web.Controllers.StudyGroups;

[ApiController]
[Route("api/groups")]
public sealed class StudyGroupsApiController(IStudyGroupService studyGroups) : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok(studyGroups.GetGroups());

    [HttpGet("{groupId}")]
    public IActionResult GetById(string groupId)
    {
        var group = studyGroups.GetGroup(groupId);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpPost("{groupId}/join")]
    public IActionResult Join(string groupId)
    {
        var group = studyGroups.Join(groupId);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpDelete("{groupId}/join")]
    public IActionResult Leave(string groupId)
    {
        try
        {
            var group = studyGroups.Leave(groupId);
            return group is null ? NotFound() : Ok(group);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{groupId}/messages")]
    public IActionResult AddMessage(string groupId, SendGroupMessageRequest request)
    {
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { message = "Nội dung tin nhắn không được để trống." });
        if (content.Length > 1000)
            return BadRequest(new { message = "Tin nhắn không được dài quá 1000 ký tự." });

        var message = studyGroups.AddMessage(groupId, content);
        return message is null
            ? BadRequest(new { message = "Bạn cần tham gia nhóm trước khi gửi tin nhắn." })
            : Ok(message);
    }

    [HttpPost]
    public IActionResult Create(CreateGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { message = "Tên nhóm và môn học là bắt buộc." });
        if (request.Name.Trim().Length > 100 || (request.Description?.Length ?? 0) > 500)
            return BadRequest(new { message = "Tên nhóm hoặc mô tả vượt quá độ dài cho phép." });

        var group = studyGroups.CreateGroup(request);
        return Created($"/api/groups/{group.Id}", group);
    }
}
