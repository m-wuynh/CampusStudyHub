using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.NotesV3.Annotations;
using StudyHub.Web.Filters;

namespace StudyHub.Web.Controllers.NotesV3;

[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(16 * 1024)]
[Route("api/notesv3/annotations")]
[ServiceFilter(typeof(NotesV3ExceptionFilter))]
public sealed class AnnotationsApiController(
    IAnnotationService annotations) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long versionId,
        [FromQuery] int pageNumber,
        [FromQuery] long? shareId,
        CancellationToken ct)
    {
        return Ok(await annotations.ListAsync(
            versionId,
            pageNumber,
            shareId,
            ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAnnotationRequest request,
        CancellationToken ct)
    {
        return Ok(await annotations.CreateAsync(
            request.DocumentVersionId,
            request.PhysicalPageNumber,
            request.Payload,
            request.GroupDocumentShareId,
            ct));
    }

    [HttpPut("{annotationId:long}")]
    public async Task<IActionResult> Update(
        long annotationId,
        [FromBody] UpdateAnnotationRequest request,
        CancellationToken ct)
    {
        return Ok(await annotations.UpdateAsync(
            annotationId,
            request.Payload,
            request.VersionToken,
            ct));
    }

    [HttpDelete("{annotationId:long}")]
    public async Task<IActionResult> Delete(
        long annotationId,
        [FromBody] DeleteAnnotationRequest request,
        CancellationToken ct)
    {
        await annotations.DeleteAsync(
            annotationId,
            request.VersionToken,
            ct);

        return NoContent();
    }

    public sealed class CreateAnnotationRequest
{
    [Range(1, long.MaxValue)]
    public long DocumentVersionId { get; init; }

    [Range(1, int.MaxValue)]
    public int PhysicalPageNumber { get; init; }

    public long? GroupDocumentShareId { get; init; }

    [Required]
    public required PdfAnnotationPayload Payload { get; init; }
}

public sealed class UpdateAnnotationRequest
{
    [Required]
    public required PdfAnnotationPayload Payload { get; init; }

    [Required]
    public required string VersionToken { get; init; }
}

public sealed class DeleteAnnotationRequest
{
    [Required]
    public required string VersionToken { get; init; }
}
}