using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.Services.Notes;

namespace StudyHub.Web.Controllers.Notes;

[ApiController]
[Authorize]
[Route("api/notes")]
public sealed class GroupNotesApiController(GroupNotesService service) : ControllerBase
{
    [HttpGet("eligible-groups")]
    public Task<IActionResult> EligibleGroups(CancellationToken ct) => Run(async () => Ok(await service.GetEligibleGroupsAsync(ct)));

    [HttpPost("documents/{documentId:long}/shares")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ShareDocument(long documentId, [FromBody] ShareToGroupRequest request, CancellationToken ct) =>
        Run(async () =>
        {
            await service.ShareDocumentAsync(documentId, request.GroupId, ct);
            return Ok(new { message = "Đã chia sẻ PDF vào nhóm." });
        });

    [HttpDelete("documents/{documentId:long}/shares/{groupId:long}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UnshareDocument(long documentId, long groupId, CancellationToken ct) =>
        Run(async () =>
        {
            await service.UnshareDocumentAsync(documentId, groupId, ct);
            return NoContent();
        });

    [HttpPost("notes/{noteId:long}/shares")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ShareNote(long noteId, [FromBody] ShareToGroupRequest request, CancellationToken ct) =>
        Run(async () =>
        {
            await service.ShareNoteSnapshotAsync(noteId, request.GroupId, ct);
            return Ok(new { message = "Đã tạo snapshot ghi chú độc lập trong nhóm." });
        });

    [HttpGet("groups/{groupId:long}/workspace")]
    public Task<IActionResult> Workspace(long groupId, CancellationToken ct) =>
        Run(async () => Ok(await service.GetWorkspaceAsync(groupId, ct)));

    [HttpGet("groups/{groupId:long}/documents/{documentId:long}")]
    public Task<IActionResult> SharedPdf(long groupId, long documentId, [FromQuery] long? versionId, CancellationToken ct) =>
        Run(async () =>
        {
            var details = await service.GetSharedPdfAsync(groupId, documentId, versionId, ct);
            return details is null ? NotFound() : Ok(details);
        });

    [HttpGet("groups/{groupId:long}/documents/{documentId:long}/file")]
    public Task<IActionResult> SharedPdfFile(long groupId, long documentId, [FromQuery] long? versionId, CancellationToken ct) =>
        Run(async () =>
        {
            var details = await service.GetSharedPdfAsync(groupId, documentId, versionId, ct);
            if (details is null) return NotFound(new { message = "Không tìm thấy PDF." });
            var path = await service.GetSharedPdfPathAsync(groupId, documentId, versionId, ct);
            return path is null ? NotFound(new { message = "PDF không còn trong kho lưu trữ." }) : PhysicalFile(path, "application/pdf", enableRangeProcessing: true);
        });

    [HttpHead("groups/{groupId:long}/documents/{documentId:long}/file")]
    public Task<IActionResult> SharedPdfMetadata(long groupId, long documentId, [FromQuery] long? versionId, CancellationToken ct) =>
        Run(async () =>
        {
            var path = await service.GetSharedPdfPathAsync(groupId, documentId, versionId, ct);
            if (path is null) return NotFound();
            Response.ContentType = "application/pdf";
            Response.ContentLength = new FileInfo(path).Length;
            Response.Headers["Accept-Ranges"] = "bytes";
            return Ok();
        });

    [HttpGet("groups/{groupId:long}/documents/{documentId:long}/annotations")]
    public Task<IActionResult> Annotations(long groupId, long documentId, [FromQuery] long versionId, CancellationToken ct) =>
        Run(async () =>
        {
            var result = await service.GetAnnotationsAsync(groupId, documentId, versionId, ct);
            return Ok(result);
        });

    [HttpPut("groups/{groupId:long}/documents/{documentId:long}/annotations")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public Task<IActionResult> SaveAnnotations(long groupId, long documentId, [FromQuery] long versionId,
        [FromBody] SaveGroupAnnotationsRequest request, CancellationToken ct) =>
        Run(async () =>
        {
            var token = await service.SaveOwnerAnnotationsAsync(groupId, documentId, versionId,
                request.Payload, request.VersionToken, ct);
            return Ok(new { versionToken = token, savedAtUtc = DateTime.UtcNow });
        });

    [HttpPost("groups/{groupId:long}/requests")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(18 * 1024 * 1024)]
    public Task<IActionResult> SubmitChange(long groupId, [FromBody] GroupChangeRequestInput request, CancellationToken ct) =>
        Run(async () => Accepted(await service.SubmitChangeAsync(groupId, request, ct)));

    [HttpPost("groups/{groupId:long}/requests/{requestId:long}/decision")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DecideChange(long groupId, long requestId, [FromBody] DecideChangeRequest request, CancellationToken ct) =>
        Run(async () =>
        {
            var result = await service.DecideChangeAsync(groupId, requestId, request.Approve, request.ResolutionNote, ct);
            return result.Status == "Conflict" ? Conflict(new { message = "Phiên bản chính thức đã thay đổi. Đề xuất được đánh dấu Conflict." }) : Ok(result);
        });

    [HttpPost("groups/{groupId:long}/notes")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public Task<IActionResult> CreateGroupNote(long groupId, [FromBody] GroupNoteMutation request, CancellationToken ct) =>
        Run(async () =>
        {
            var result = await service.CreateGroupNoteAsync(groupId, request, ct);
            return result.Applied ? Ok(result) : Accepted(result);
        });

    [HttpPut("groups/{groupId:long}/notes/{noteId:long}")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public Task<IActionResult> SaveGroupNote(long groupId, long noteId, [FromBody] GroupNoteMutation request,
        [FromQuery] string? operation, CancellationToken ct) =>
        Run(async () =>
        {
            var result = await service.SaveGroupNoteAsync(groupId, noteId, request, operation, ct);
            return result.Applied ? Ok(result) : Accepted(result);
        });

    [HttpPost("groups/{groupId:long}/notes/{noteId:long}/archive")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ArchiveGroupNote(long groupId, long noteId, CancellationToken ct) =>
        SetGroupNoteArchive(groupId, noteId, true, ct);

    [HttpPost("groups/{groupId:long}/notes/{noteId:long}/restore")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreGroupNote(long groupId, long noteId, CancellationToken ct) =>
        SetGroupNoteArchive(groupId, noteId, false, ct);

    private Task<IActionResult> SetGroupNoteArchive(long groupId, long noteId, bool archived, CancellationToken ct) =>
        Run(async () => await service.SetGroupNoteArchiveAsync(groupId, noteId, archived, ct)
            ? NoContent() : NotFound());

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (GroupNotesException exception)
        {
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
    }
}

public sealed record ShareToGroupRequest(long GroupId);
public sealed record SaveGroupAnnotationsRequest(JsonElement Payload, string? VersionToken);
public sealed record DecideChangeRequest(bool Approve, string? ResolutionNote);
