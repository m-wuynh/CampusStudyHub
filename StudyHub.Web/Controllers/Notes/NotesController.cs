using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StudyHub.BLL.DTOs.Notes;
using StudyHub.BLL.Services.Notes;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.Notes;
using StudyHub.Web.ViewModels.Notes;

namespace StudyHub.Web.Controllers.Notes;

[Route("Notes")]
[AutoValidateAntiforgeryToken]
public sealed class NotesController : Controller
{
    private readonly INotesWorkspaceService service;
    private readonly INotebookService notebooks;
    private readonly NotesUserResolver users;
    private readonly IWebHostEnvironment environment;
    private NotesUserDto current = null!;

    public NotesController(
        IRepository repository,
        IWebHostEnvironment environment)
    {
        this.environment = environment;
        users = new NotesUserResolver(repository);
        notebooks = new NotebookService(repository);

        service = new NotesWorkspaceService(
            repository,
            new NotesFileRepository(
                Path.Combine(
                    environment.ContentRootPath,
                    "App_Data",
                    "Notes")));
    }

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        try
        {
            current = await users.ResolveAsync(
                User.Identity?.IsAuthenticated == true,
                User.FindFirst(NotesUserResolver.UserIdClaim)?.Value,
                environment.IsDevelopment(),
                HttpContext.RequestAborted);

            ViewData["NotesUser"] = current;
        }
        catch (NotesException ex)
        {
            context.Result = Problem(
                title: ex.Message,
                statusCode: ex.Status);
            return;
        }

        var executed = await next();

        if (executed.Exception is NotesException error)
        {
            executed.ExceptionHandled = true;
            executed.Result = Problem(
                title: error.Message,
                statusCode: error.Status);
        }
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        CancellationToken ct)
    {
        return View(new NotesWorkspaceViewModel(
            current,
            await service.ListAsync(current.Id, search, ct),
            search));
    }

    [HttpGet("Edit/{id:long?}")]
    public async Task<IActionResult> Edit(
        long? id,
        Guid? pageId,
        CancellationToken ct)
    {
        return View(await notebooks.GetAsync(
            current.Id, id, pageId, ct));
    }

    [HttpPost("Edit/{id:long?}")]
    public async Task<IActionResult> Edit(
        [FromForm] NotebookEditDto dto,
        string command = "save",
        CancellationToken ct = default)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var result = await notebooks.SaveAsync(
                    current.Id, dto, command, ct);

                return RedirectToAction(nameof(Edit), new
                {
                    id = result.Id,
                    pageId = result.PageId
                });
            }
            catch (NotesException ex)
            {
                Response.StatusCode = ex.Status;
                ModelState.AddModelError("", ex.Message);
            }
        }

        // Chỉ nạp lại danh sách trang.
        // Giữ nguyên nội dung vừa nhập và Version để tránh ghi đè xung đột.
        var persisted = await notebooks.GetAsync(
            current.Id, dto.Id, null, ct);

        dto.Pages = persisted.Pages;

        return View(dto);
    }

    [HttpPost("Delete/{id:long}")]
    public async Task<IActionResult> Delete(
        long id,
        string version,
        CancellationToken ct)
    {
        await service.DeleteTextAsync(
            current.Id, id, version, ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Pin/{id:long}")]
    public async Task<IActionResult> TogglePin(
        long id,
        CancellationToken ct)
    {
        await service.TogglePinAsync(current.Id, id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Upload")]
    [RequestSizeLimit(22 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 22 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        CancellationToken ct)
    {
        try
        {
            if (file == null
                || file.Length == 0
                || file.Length > NotesWorkspaceService.MaxUpload)
            {
                throw new NotesException(
                    "Chọn PDF từ 1 byte đến 20 MiB.");
            }

            await using var input = file.OpenReadStream();

            var id = await service.UploadAsync(
                current.Id, file.FileName, input, ct);

            return RedirectToAction(nameof(Reader), new { id });
        }
        catch (NotesException ex)
        {
            TempData["NotesError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("Pdf/Rename/{id:long}")]
    public async Task<IActionResult> RenamePdf(
        long id,
        string title,
        CancellationToken ct)
    {
        await service.RenamePdfAsync(
            current.Id, id, title, ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Pdf/Delete/{id:long}")]
    public async Task<IActionResult> DeletePdf(
        long id,
        CancellationToken ct)
    {
        await service.DeletePdfAsync(current.Id, id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Pdf/Pin/{id:long}")]
    public async Task<IActionResult> TogglePdfPin(
        long id,
        CancellationToken ct)
    {
        await service.TogglePdfPinAsync(current.Id, id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Reader/{id:long}")]
    public async Task<IActionResult> Reader(
        long id,
        CancellationToken ct)
    {
        return View(await service.GetPdfAsync(current.Id, id, ct));
    }

    [HttpGet("Pdf/{id:long}")]
    public async Task<IActionResult> Pdf(
        long id,
        CancellationToken ct)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, max-age=3600";

        return File(
            await service.OpenPdfAsync(current.Id, id, ct),
            "application/pdf",
            enableRangeProcessing: true);
    }

    [HttpGet("Page/{id:long}/{page:int}")]
    public async Task<IActionResult> Page(
        long id,
        int page,
        CancellationToken ct)
    {
        return Json(await service.GetPageAsync(
            current.Id, id, page, ct));
    }

    [HttpPost("Page/{id:long}/{page:int}")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Page(
        long id,
        int page,
        [FromBody] PdfPageNoteDto? dto,
        CancellationToken ct)
    {
        if (dto == null || !ModelState.IsValid)
        {
            return BadRequest(new
            {
                title = "Nội dung gửi lên không hợp lệ."
            });
        }

        return Json(await service.SavePageAsync(
            current.Id, id, page, dto, ct));
    }
}
