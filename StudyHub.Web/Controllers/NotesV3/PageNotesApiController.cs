using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.NotesV3.PageNotes;
using StudyHub.Web.Filters;

namespace StudyHub.Web.Controllers.NotesV3;

[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/notesv3/pdfs/versions/{versionId:long}")]
[ServiceFilter(typeof(NotesV3ExceptionFilter))]
public sealed class PageNotesApiController(
    IPageNotesService pageNotes) : ControllerBase
{
    [HttpGet("pages/{pageNumber:int}/note")]
    public async Task<IActionResult> GetNote(
        long versionId,
        int pageNumber,
        [FromQuery] long? shareId,
        CancellationToken ct)
    {
        return Ok(await pageNotes.GetAsync(
            versionId,
            pageNumber,
            shareId,
            ct));
    }

    [HttpPut("pages/{pageNumber:int}/note")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> SavePersonalNote(
        long versionId,
        int pageNumber,
        [FromBody] SavePdfPageNoteRequest request,
        CancellationToken ct)
    {
        // API này chỉ dành cho ghi chú cá nhân.
        // Ghi chú nhóm sẽ đi qua Hub/Yjs.
        return Ok(await pageNotes.SavePersonalAsync(
            versionId,
            pageNumber,
            request,
            ct));
    }

    [HttpGet("book")]
    public async Task<IActionResult> Book(
        long versionId,
        [FromQuery] long? shareId,
        CancellationToken ct)
    {
        return Ok(await pageNotes.ListBookPagesAsync(
            versionId,
            shareId,
            ct));
    }
}