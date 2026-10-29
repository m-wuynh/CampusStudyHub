using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.NotesV3.References;
using StudyHub.Web.Filters;

namespace StudyHub.Web.Controllers.NotesV3;

[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
[Route("api/notesv3/references")]
[ServiceFilter(typeof(NotesV3ExceptionFilter))]
public sealed class ReferencesApiController(
    IReferenceService references) : ControllerBase
{
    [HttpGet("page/{pageId:long}")]
    public async Task<IActionResult> List(
        long pageId,
        CancellationToken ct)
    {
        return Ok(await references.ListAsync(pageId, ct));
    }

    [HttpGet("{referenceId:long}/source")]
    public async Task<IActionResult> Resolve(
        long referenceId,
        CancellationToken ct)
    {
        return Ok(await references.ResolveAsync(
            referenceId,
            ct));
    }

    [HttpPost("page/{pageId:long}/capture")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> CapturePersonal(
        long pageId,
        [FromBody] PersonalCaptureRequest request,
        CancellationToken ct)
    {
        return Ok(await references.CapturePersonalAsync(
            pageId,
            request,
            ct));
    }

    [HttpPost(
        "~/api/notesv3/pdfs/versions/{versionId:long}/pages/{pageNumber:int}/capture")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> CapturePersonalPdf(
        long versionId,
        int pageNumber,
        [FromBody] PersonalCaptureRequest request,
        CancellationToken ct)
    {
        return Ok(await references.CapturePersonalPdfAsync(
            versionId,
            pageNumber,
            request,
            ct));
    }
}