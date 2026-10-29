using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.NotesV3.Pdfs;
using StudyHub.Web.Filters;

namespace StudyHub.Web.Controllers.NotesV3;

[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/notesv3/pdfs")]
[ServiceFilter(typeof(NotesV3ExceptionFilter))]
public sealed class PdfsApiController(
    IPdfService pdfs) : ControllerBase
{
    private const long MaxPdfBytes = 25L * 1024 * 1024;
    private const long MaxRequestBytes = 27L * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeArchived,
        CancellationToken ct)
    {
        return Ok(await pdfs.ListAsync(includeArchived, ct));
    }

    [HttpGet("{documentId:long}")]
    public async Task<IActionResult> GetMetadata(
        long documentId,
        [FromQuery] long? shareId,
        CancellationToken ct)
    {
        return Ok(await pdfs.GetAsync(
            documentId,
            shareId,
            ct));
    }

    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Upload(
        [FromForm] UploadPdfRequest request,
        CancellationToken ct)
    {
        if (request.File.Length == 0)
        {
            return Problem(
                statusCode: 400,
                title: "File PDF rỗng.");
        }

        if (request.File.Length > MaxPdfBytes)
        {
            return Problem(
                statusCode: 413,
                title: "PDF tối đa 25 MiB.");
        }

        await using var stream = request.File.OpenReadStream();

        var result = await pdfs.UploadAsync(
            stream,
            request.File.FileName,
            request.Title,
            ct);

        return CreatedAtAction(
            nameof(GetMetadata),
            new { documentId = result.DocumentId },
            result);
    }

    [HttpGet("versions/{versionId:long}/file")]
    public async Task<IActionResult> OpenFile(
        long versionId,
        [FromQuery] long? shareId,
        CancellationToken ct)
    {
        var result = await pdfs.OpenFileAsync(
            versionId,
            shareId,
            ct);

        var disposition = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = result.FileName
        };

        Response.Headers["Content-Disposition"] =
            disposition.ToString();

        // Không dùng await using tại đây:
        // ASP.NET Core quản lý stream của FileStreamResult.
        return File(
            result.Content,
            "application/pdf",
            enableRangeProcessing: true);
    }

    [HttpPatch("{documentId:long}/title")]
    public async Task<IActionResult> Rename(
        long documentId,
        [FromBody] RenamePdfRequest request,
        CancellationToken ct)
    {
        return Ok(await pdfs.RenameAsync(
            documentId,
            request.Title,
            request.VersionToken,
            ct));
    }

    [HttpPut("{documentId:long}/archive")]
    public async Task<IActionResult> SetArchived(
        long documentId,
        [FromBody] ArchivePdfRequest request,
        CancellationToken ct)
    {
        return Ok(await pdfs.SetArchivedAsync(
            documentId,
            request.Archived,
            request.VersionToken,
            ct));
    }

    public sealed class UploadPdfRequest
    {
        [Required]
        public IFormFile File { get; init; } = null!;

        [StringLength(200)]
        public string? Title { get; init; }
    }

    public sealed class RenamePdfRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Title { get; init; }

    [Required]
    public required string VersionToken { get; init; }
}

public sealed class ArchivePdfRequest
{
    public bool Archived { get; init; }

    [Required]
    public required string VersionToken { get; init; }
}
}