using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.NotesV3.Notebooks;
using StudyHub.BLL.Services.NotesV3.Sharing;
using StudyHub.Web.Filters;

namespace StudyHub.Web.Controllers.NotesV3;

[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/notesv3/groups")]
[ServiceFilter(typeof(NotesV3ExceptionFilter))]
public sealed class GroupSharesApiController(
    IGroupShareService sharing,
    INotebookService notebooks) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> EligibleGroups(
        CancellationToken ct)
    {
        return Ok(await sharing.ListEligibleGroupsAsync(ct));
    }

    [HttpGet("{groupId:long}/resources")]
    public async Task<IActionResult> Resources(
        long groupId,
        CancellationToken ct)
    {
        var pdfs = await sharing.ListSharedPdfsAsync(
            groupId,
            ct);

        var groupNotebooks = await notebooks.ListAsync(
            groupId,
            search: null,
            ct: ct);

        return Ok(new
        {
            pdfs,
            notebooks = groupNotebooks
        });
    }

    [HttpPost("{groupId:long}/pdf-shares")]
    public async Task<IActionResult> SharePdf(
        long groupId,
        [FromBody] SharePdfRequest request,
        CancellationToken ct)
    {
        return Ok(await sharing.SharePdfAsync(
            request.DocumentId,
            groupId,
            ct));
    }

    [HttpDelete("~/api/notesv3/pdf-shares/{shareId:long}")]
    public async Task<IActionResult> UnsharePdf(
        long shareId,
        [FromBody] UnsharePdfRequest request,
        CancellationToken ct)
    {
        await sharing.UnsharePdfAsync(
            shareId,
            request.VersionToken,
            ct);

        return NoContent();
    }

    [HttpPost("{groupId:long}/notebook-copies")]
    [RequestSizeLimit(24 * 1024 * 1024)]
    public async Task<IActionResult> CopyNotebook(
        long groupId,
        [FromBody] CopyNotebookToGroupRequest request,
        CancellationToken ct)
    {
        var result = await sharing.CopyNotebookAsync(
            new CopyNotebookRequest(
                request.SourceNotebookId,
                groupId,
                request.Seeds),
            ct);

        return Ok(result);
    }

    public sealed class SharePdfRequest
{
    [Range(1, long.MaxValue)]
    public long DocumentId { get; init; }
}

public sealed class UnsharePdfRequest
{
    [Required]
    public required string VersionToken { get; init; }
}

public sealed class CopyNotebookToGroupRequest
{
    [Range(1, long.MaxValue)]
    public long SourceNotebookId { get; init; }

    [Required]
    public required IReadOnlyList<NotebookPageSeed> Seeds
    {
        get;
        init;
    }
}
}