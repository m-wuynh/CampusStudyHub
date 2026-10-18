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

    [HttpGet("documents/{documentId:long}/shares")]
    public Task<IActionResult> DocumentShares(long documentId, CancellationToken ct) =>
        Run(async () => Ok(await service.GetDocumentSharesAsync(documentId, ct)));

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
            var token = await service.SaveGroupAnnotationsAsync(groupId, documentId, versionId,
                request.ShareId, request.Payload, request.VersionToken, ct);
            return Ok(new { versionToken = token, savedAtUtc = DateTime.UtcNow });
        });

    [HttpPost("groups/{groupId:long}/requests")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(18 * 1024 * 1024)]
    public Task<IActionResult> SubmitChange(long groupId, [FromBody] GroupChangeRequestInput request, CancellationToken ct) =>
        Task.FromResult<IActionResult>(StatusCode(410, new { message = "Đề xuất chỉnh sửa đã ngừng sử dụng. Thành viên Active sửa trực tiếp." }));

    [HttpPost("groups/{groupId:long}/requests/{requestId:long}/decision")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DecideChange(long groupId, long requestId, [FromBody] DecideChangeRequest request, CancellationToken ct) =>
        Task.FromResult<IActionResult>(StatusCode(410, new { message = "Duyệt đề xuất đã ngừng sử dụng. Dữ liệu cũ được giữ để đối chiếu." }));

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

    [HttpPost("groups/{groupId:long}/notes/{noteId:long}/copy-personal")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CopyGroupNoteToPersonal(long groupId, long noteId, CancellationToken ct) =>
        Run(async () => Ok(new { id = await service.CopyGroupNoteToPersonalAsync(groupId, noteId, ct) }));

    [HttpPost("groups/{groupId:long}/notes/{noteId:long}/restore-copy")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreDeletedGroupNoteAsCopy(long groupId, long noteId,
        [FromBody] RestoreGroupNoteCopyRequest request, CancellationToken ct) =>
        Run(async () => Ok(await service.RestoreDeletedNoteAsCopyAsync(groupId, noteId, request.VersionToken, ct)));

    [HttpGet("groups/{groupId:long}/notes/{noteId:long}/revisions")]
    public Task<IActionResult> NoteRevisions(long groupId, long noteId, CancellationToken ct) =>
        Run(async () => Ok(await service.GetNoteRevisionsAsync(groupId, noteId, ct)));

    [HttpPost("groups/{groupId:long}/notes/{noteId:long}/revisions/{revisionId:long}/restore-copy")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreNoteRevisionAsCopy(long groupId, long noteId, long revisionId, CancellationToken ct) =>
        Run(async () => Ok(await service.RestoreRevisionAsCopyAsync(groupId, noteId, revisionId, ct)));

    [HttpPost("groups/{groupId:long}/notes/{noteId:long}/archive")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ArchiveGroupNote(long groupId, long noteId,
        [FromBody] GroupNoteVersionRequest request, CancellationToken ct) =>
        SetGroupNoteArchive(groupId, noteId, true, request.VersionToken, ct);

    [HttpPost("groups/{groupId:long}/notes/{noteId:long}/restore")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreGroupNote(long groupId, long noteId,
        [FromBody] GroupNoteVersionRequest request, CancellationToken ct) =>
        SetGroupNoteArchive(groupId, noteId, false, request.VersionToken, ct);

    private Task<IActionResult> SetGroupNoteArchive(
        long groupId, long noteId, bool archived, string? versionToken, CancellationToken ct) =>
        Run(async () => await service.SetGroupNoteArchiveAsync(groupId, noteId, archived, versionToken, ct)
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
public sealed record SaveGroupAnnotationsRequest(long ShareId, JsonElement Payload, string? VersionToken);
public sealed record DecideChangeRequest(bool Approve, string? ResolutionNote);
public sealed record RestoreGroupNoteCopyRequest(string? VersionToken);
public sealed record GroupNoteVersionRequest(string? VersionToken);
