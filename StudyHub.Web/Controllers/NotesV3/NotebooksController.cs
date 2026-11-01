using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.BLL.Services.NotesV3.Notebooks;
using StudyHub.Web.ViewModels.NotesV3;

namespace StudyHub.Web.Controllers.NotesV3;

[Authorize]
[AutoValidateAntiforgeryToken]
[Route("NotesV3/Notebooks")]
public sealed class NotebooksController(
    INotebookService notebooks) : Controller
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Notebook(
        long id,
        long? pageId,
        long? sectionId,
        CancellationToken ct)
    {
        var data = await notebooks.GetScreenAsync(id, pageId, sectionId, ct);

        var sections = data.Sections
            .Select(section => new SectionViewModel(
                section.Id,
                section.Title,
                section.Pages
                    .Select(page => new PageLinkViewModel(
                        page.Id,
                        page.Title))
                    .ToList()))
            .ToList();

        var currentPage = data.CurrentPage is null
            ? null
            : new PageEditorViewModel(
                data.CurrentPage.Id,
                data.CurrentPage.Title,
                data.CurrentPage.Text,
                data.CurrentPage.VersionToken);

        var model = new NotesNotebookViewModel(
            data.Id,
            data.Title,
            sections,
            data.SelectedSectionId,
            currentPage);

        return View("~/Views/NotesV3/Notebook.cshtml", model);
    }

    [HttpPost("CreateNotebook")]
    public async Task<IActionResult> CreateNotebook(
        string? title,
        long? groupId,
        CancellationToken ct)
    {
        if (InvalidTitle(title))
        {
            return IndexError("Tên notebook phải có từ 1 đến 200 ký tự.", groupId);
        }

        try
        {
            var id = await notebooks.CreateNotebookAsync(
                title!.Trim(),
                groupId,
                ct);

            return RedirectToAction(nameof(Notebook), new { id });
        }
        catch (NotesV3Exception ex)
        {
            return IndexError(ex.Message, groupId);
        }
    }

    [HttpPost("RenameNotebook")]
    public async Task<IActionResult> RenameNotebook(
        long id,
        string? title,
        CancellationToken ct)
    {
        if (InvalidTitle(title))
        {
            return IndexError("Tên notebook phải có từ 1 đến 200 ký tự.");
        }

        try
        {
            await notebooks.RenameNotebookAsync(id, title!.Trim(), ct);
            TempData["Success"] = "Đã đổi tên notebook.";
            return RedirectToAction("Index", "NotesV3");
        }
        catch (NotesV3Exception ex)
        {
            return IndexError(ex.Message);
        }
    }

    [HttpPost("DeleteNotebook")]
    public async Task<IActionResult> DeleteNotebook(
        long id,
        CancellationToken ct)
    {
        try
        {
            await notebooks.DeleteNotebookAsync(id, ct);
            TempData["Success"] = "Đã xóa notebook.";
            return RedirectToAction("Index", "NotesV3");
        }
        catch (NotesV3Exception ex)
        {
            return IndexError(ex.Message);
        }
    }

    [HttpPost("RestoreNotebook")]
    public async Task<IActionResult> RestoreNotebook(long id, CancellationToken ct)
    {
        try
        {
            await notebooks.RestoreNotebookAsync(id, ct);
            TempData["Success"] = "Đã khôi phục notebook.";
        }
        catch (NotesV3Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Trash", "NotesV3");
    }

    [HttpPost("CreateSection")]
    public async Task<IActionResult> CreateSection(
        long notebookId,
        string? title,
        CancellationToken ct)
    {
        if (InvalidTitle(title))
        {
            return NotebookError(notebookId, "Tên section phải có từ 1 đến 200 ký tự.");
        }

        try
        {
            var newSectionId = await notebooks.CreateSectionAsync(notebookId, title!.Trim(), ct);
            TempData["Success"] = "Đã thêm section.";
            return RedirectToAction(nameof(Notebook), new { id = notebookId, sectionId = newSectionId });
        }
        catch (NotesV3Exception ex)
        {
            return NotebookError(notebookId, ex.Message);
        }
    }

    [HttpPost("RenameSection")]
    public async Task<IActionResult> RenameSection(
        long notebookId,
        long sectionId,
        string? title,
        CancellationToken ct)
    {
        if (InvalidTitle(title))
        {
            return NotebookError(notebookId, "Tên section phải có từ 1 đến 200 ký tự.");
        }

        try
        {
            await notebooks.RenameSectionAsync(sectionId, title!.Trim(), ct);
            TempData["Success"] = "Đã đổi tên section.";
            return RedirectToAction(nameof(Notebook), new { id = notebookId, sectionId });
        }
        catch (NotesV3Exception ex)
        {
            return NotebookError(notebookId, ex.Message);
        }
    }

    [HttpPost("DeleteSection")]
    public async Task<IActionResult> DeleteSection(
        long notebookId,
        long sectionId,
        CancellationToken ct)
    {
        try
        {
            await notebooks.DeleteSectionAsync(sectionId, ct);
            TempData["Success"] = "Đã xóa section.";
            return RedirectToAction(nameof(Notebook), new { id = notebookId });
        }
        catch (NotesV3Exception ex)
        {
            return NotebookError(notebookId, ex.Message);
        }
    }

    private static bool InvalidTitle(string? title) =>
        string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200;

    private IActionResult IndexError(string message, long? groupId = null)
    {
        TempData["Error"] = message;
        return RedirectToAction("Index", "NotesV3", new { groupId });
    }

    private IActionResult NotebookError(long notebookId, string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Notebook), new { id = notebookId });
    }
}
