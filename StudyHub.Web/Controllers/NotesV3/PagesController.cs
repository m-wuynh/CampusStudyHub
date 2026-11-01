using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.BLL.Services.NotesV3.Pages;

namespace StudyHub.Web.Controllers.NotesV3;

[Authorize]
[AutoValidateAntiforgeryToken]
[Route("NotesV3/Pages")]
public sealed class PagesController(IPageService pages) : Controller
{
    [HttpPost("CreatePage")]
    public async Task<IActionResult> CreatePage(
        long notebookId,
        long sectionId,
        string? title,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
        {
            return NotebookError(notebookId, "Tên trang phải có từ 1 đến 200 ký tự.");
        }

        try
        {
            var pageId = await pages.CreateAsync(sectionId, title.Trim(), ct);
            return RedirectToNotebook(notebookId, pageId);
        }
        catch (NotesV3Exception ex)
        {
            return NotebookError(notebookId, ex.Message);
        }
    }

    [HttpPost("SavePage")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> SavePage(
        long notebookId,
        long pageId,
        string? text,
        string? versionToken,
        CancellationToken ct)
    {
        try
        {
            await pages.SaveTextAsync(pageId, text ?? string.Empty, versionToken, ct);
            TempData["Success"] = "Đã lưu ghi chú.";
            return RedirectToNotebook(notebookId, pageId);
        }
        catch (NotesV3Exception ex)
        {
            return NotebookError(notebookId, ex.Message, pageId);
        }
    }

    [HttpPost("RenamePage")]
    public async Task<IActionResult> RenamePage(
        long notebookId,
        long pageId,
        string? title,
        string? versionToken,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
        {
            return NotebookError(notebookId, "Tên trang phải có từ 1 đến 200 ký tự.", pageId);
        }

        try
        {
            await pages.RenameAsync(pageId, title.Trim(), versionToken, ct);
            TempData["Success"] = "Đã đổi tên trang.";
            return RedirectToNotebook(notebookId, pageId);
        }
        catch (NotesV3Exception ex)
        {
            return NotebookError(notebookId, ex.Message, pageId);
        }
    }

    [HttpPost("DeletePage")]
    public async Task<IActionResult> DeletePage(
        long notebookId,
        long pageId,
        string? versionToken,
        CancellationToken ct)
    {
        try
        {
            await pages.DeleteAsync(pageId, versionToken, ct);
            TempData["Success"] = "Đã xóa trang.";
            return RedirectToNotebook(notebookId);
        }
        catch (NotesV3Exception ex)
        {
            return NotebookError(notebookId, ex.Message, pageId);
        }
    }

    private IActionResult RedirectToNotebook(long notebookId, long? pageId = null) =>
        RedirectToAction("Notebook", "Notebooks", new { id = notebookId, pageId });

    private IActionResult NotebookError(
        long notebookId,
        string message,
        long? pageId = null)
    {
        TempData["Error"] = message;
        return RedirectToNotebook(notebookId, pageId);
    }
}
