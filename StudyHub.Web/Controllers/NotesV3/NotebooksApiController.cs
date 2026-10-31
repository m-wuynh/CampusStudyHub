using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.NotesV3.Notebooks;
using StudyHub.Web.Filters;

namespace StudyHub.Web.Controllers.NotesV3;

[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[Route("api/notesv3/notebooks")]
[ServiceFilter(typeof(NotesV3ExceptionFilter))]
public sealed class NotebooksApiController(
    INotebookService notebooks) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? groupId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await notebooks.ListAsync(
            groupId,
            search,
            ct);

        return Ok(result);
    }

    [HttpGet("{notebookId:long}")]
    public async Task<IActionResult> GetNotebook(
        long notebookId,
        CancellationToken ct)
    {
        var result = await notebooks.GetAsync(
            notebookId,
            ct);

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotebook(
        [FromBody] CreateNotebookRequest request,
        CancellationToken ct)
    {
        var id = await notebooks.CreateNotebookAsync(
            request.Title,
            request.GroupId,
            ct);

        return CreatedAtAction(
            nameof(GetNotebook),
            new { notebookId = id },
            new { id });
    }

    [HttpPatch("{notebookId:long}/title")]
    public async Task<IActionResult> RenameNotebook(
        long notebookId,
        [FromBody] TitleRequest request,
        CancellationToken ct)
    {
        await notebooks.RenameNotebookAsync(
            notebookId,
            request.Title,
            ct);

        return NoContent();
    }

    [HttpDelete("{notebookId:long}")]
    public async Task<IActionResult> DeleteNotebook(
        long notebookId,
        CancellationToken ct)
    {
        await notebooks.DeleteNotebookAsync(
            notebookId,
            ct);

        return NoContent();
    }

    [HttpPut("order")]
    public async Task<IActionResult> ReorderNotebooks(
        [FromBody] NotebookOrderRequest request,
        CancellationToken ct)
    {
        await notebooks.ReorderNotebooksAsync(
            request.GroupId,
            request.OrderedIds,
            ct);

        return NoContent();
    }

    [HttpPost("{notebookId:long}/sections")]
    public async Task<IActionResult> CreateSection(
        long notebookId,
        [FromBody] TitleRequest request,
        CancellationToken ct)
    {
        var id = await notebooks.CreateSectionAsync(
            notebookId,
            request.Title,
            ct);

        return Ok(new { id });
    }

    [HttpPatch(
        "~/api/notesv3/sections/{sectionId:long}/title")]
    public async Task<IActionResult> RenameSection(
        long sectionId,
        [FromBody] TitleRequest request,
        CancellationToken ct)
    {
        await notebooks.RenameSectionAsync(
            sectionId,
            request.Title,
            ct);

        return NoContent();
    }

    [HttpDelete(
        "~/api/notesv3/sections/{sectionId:long}")]
    public async Task<IActionResult> DeleteSection(
        long sectionId,
        CancellationToken ct)
    {
        await notebooks.DeleteSectionAsync(
            sectionId,
            ct);

        return NoContent();
    }

    [HttpPut("{notebookId:long}/sections/order")]
    public async Task<IActionResult> ReorderSections(
        long notebookId,
        [FromBody] OrderRequest request,
        CancellationToken ct)
    {
        await notebooks.ReorderSectionsAsync(
            notebookId,
            request.OrderedIds,
            ct);

        return NoContent();
    }

    [HttpPost(
        "~/api/notesv3/sections/{sectionId:long}/pages")]
    public async Task<IActionResult> CreatePage(
        long sectionId,
        [FromBody] TitleRequest request,
        CancellationToken ct)
    {
        var id = await notebooks.CreatePageAsync(
            sectionId,
            request.Title,
            ct);

        return CreatedAtAction(
            nameof(GetPage),
            new { pageId = id },
            new { id });
    }

    [HttpGet("~/api/notesv3/pages/{pageId:long}")]
    public async Task<IActionResult> GetPage(
        long pageId,
        CancellationToken ct)
    {
        var result = await notebooks.GetPageAsync(
            pageId,
            ct);

        return Ok(result);
    }

    [HttpPut("~/api/notesv3/pages/{pageId:long}")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> SavePage(
        long pageId,
        [FromBody] PageSaveRequest request,
        CancellationToken ct)
    {
        var result = await notebooks.SavePersonalPageAsync(
            pageId,
            request,
            ct);

        return Ok(result);
    }

    [HttpPatch(
        "~/api/notesv3/pages/{pageId:long}/title")]
    public async Task<IActionResult> RenamePage(
        long pageId,
        [FromBody] PageTitleRequest request,
        CancellationToken ct)
    {
        await notebooks.RenamePageAsync(
            pageId,
            request.Title,
            request.VersionToken,
            ct);

        return NoContent();
    }

    [HttpDelete("~/api/notesv3/pages/{pageId:long}")]
    public async Task<IActionResult> DeletePage(
        long pageId,
        [FromBody] VersionRequest request,
        CancellationToken ct)
    {
        await notebooks.DeletePageAsync(
            pageId,
            request.VersionToken,
            ct);

        return NoContent();
    }

    [HttpGet("{notebookId:long}/trash")]
    public async Task<IActionResult> Trash(
        long notebookId,
        CancellationToken ct)
    {
        var result = await notebooks.ListTrashAsync(
            notebookId,
            ct);

        return Ok(result);
    }

    [HttpPost(
        "~/api/notesv3/pages/{pageId:long}/restore")]
    public async Task<IActionResult> RestorePage(
        long pageId,
        [FromBody] VersionRequest request,
        CancellationToken ct)
    {
        await notebooks.RestorePageAsync(
            pageId,
            request.VersionToken,
            ct);

        return NoContent();
    }

    [HttpPost(
        "~/api/notesv3/pages/{pageId:long}/move")]
    public async Task<IActionResult> MovePage(
        long pageId,
        [FromBody] MovePageRequest request,
        CancellationToken ct)
    {
        await notebooks.MovePageAsync(
            pageId,
            request.TargetSectionId,
            request.VersionToken,
            ct);

        return NoContent();
    }

    [HttpPut(
        "~/api/notesv3/sections/{sectionId:long}/pages/order")]
    public async Task<IActionResult> ReorderPages(
        long sectionId,
        [FromBody] OrderRequest request,
        CancellationToken ct)
    {
        await notebooks.ReorderPagesAsync(
            sectionId,
            request.OrderedIds,
            ct);

        return NoContent();
    }

    public sealed class CreateNotebookRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public required string Title { get; init; }

        public long? GroupId { get; init; }
    }

    public sealed class TitleRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public required string Title { get; init; }
    }

    public sealed class PageTitleRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public required string Title { get; init; }

        [Required]
        public required string VersionToken { get; init; }
    }

    public sealed class VersionRequest
    {
        [Required]
        public required string VersionToken { get; init; }
    }

    public sealed class OrderRequest
    {
        [Required]
        public required long[] OrderedIds { get; init; }
    }

    public sealed class NotebookOrderRequest
    {
        public long? GroupId { get; init; }

        [Required]
        public required long[] OrderedIds { get; init; }
    }

    public sealed class MovePageRequest
    {
        [Range(1, long.MaxValue)]
        public long TargetSectionId { get; init; }

        [Required]
        public required string VersionToken { get; init; }
    }
}