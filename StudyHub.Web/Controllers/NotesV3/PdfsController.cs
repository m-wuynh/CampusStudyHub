using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.BLL.Services.NotesV3.Annotations;
using StudyHub.BLL.Services.NotesV3.PagesNotes;
using StudyHub.BLL.Services.NotesV3.Pdfs;
using StudyHub.BLL.Services.NotesV3.Sharing;
using StudyHub.Web.ViewModels.NotesV3;

namespace StudyHub.Web.Controllers.NotesV3;

[Authorize]
[AutoValidateAntiforgeryToken]
[Route("NotesV3/Pdfs")]
public sealed class PdfsController(
    IPdfService pdfs,
    IPageNotesService pageNotes,
    IPdfMarksService marks,
    IGroupShareService sharing) : Controller
{
    private const long MaxPdfBytes = 25L * 1024 * 1024;

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Reader(
        long id,
        int page = 1,
        long? shareId = null,
        CancellationToken ct = default)
    {
        var model = await GetReaderModelAsync(id, page, shareId, ct);
        return View("~/Views/NotesV3/Reader.cshtml", model);
    }

    [HttpGet("{id:long}/Viewer")]
    public async Task<IActionResult> Viewer(
        long id,
        int page = 1,
        long? shareId = null,
        CancellationToken ct = default)
    {
        var model = await GetReaderModelAsync(id, page, shareId, ct);
        return View("~/Views/NotesV3/PdfViewer.cshtml", model);
    }

    private async Task<NotesReaderViewModel> GetReaderModelAsync(
        long id, int page, long? shareId, CancellationToken ct)
    {
        var pdf = await pdfs.GetAsync(id, shareId, ct);
        var pageCount = pdf.PageCount;

        if (page < 1 || page > pageCount)
        {
            throw new NotesV3Exception(400, "Trang nằm ngoài tài liệu PDF.");
        }

        var note = await pageNotes.GetTextAsync(
            pdf.DocumentVersionId,
            page,
            shareId,
            ct);
        var pageMarks = await marks.ListAsync(
            pdf.DocumentVersionId, page, shareId, ct);

        var model = new NotesReaderViewModel(
            pdf.DocumentId,
            pdf.DocumentVersionId,
            pdf.Title,
            pdf.FileName,
            pageCount,
            page,
            note.Text,
            note.VersionToken,
            pdf.IsArchived,
            shareId,
            pageMarks.Select(item => new PdfMarkViewModel(
                item.Id,
                item.Mark.Kind,
                item.Mark.Color,
                item.Mark.X,
                item.Mark.Y,
                item.Mark.Width,
                item.Mark.Height,
                item.Mark.Points?.Select(point =>
                    new MarkPointViewModel(point.X, point.Y)).ToList()
                    ?? [],
                item.VersionToken,
                item.Mark.Text)).ToList());

        return model;
    }

    [HttpGet("{id:long}/Book")]
    public async Task<IActionResult> Book(
        long id,
        int page = 1,
        long? shareId = null,
        CancellationToken ct = default)
    {
        var pdf = await pdfs.GetAsync(id, shareId, ct);
        var pages = await pageNotes.ListBookTextAsync(
            pdf.DocumentVersionId,
            shareId,
            ct);

        var model = new NotesBookViewModel(
            pdf.DocumentId,
            pdf.Title,
            shareId,
            Math.Clamp(page, 1, pdf.PageCount),
            pages.Select(item => new BookPageViewModel(
                item.PageNumber,
                item.Text)).ToList());

        return View("~/Views/NotesV3/Book.cshtml", model);
    }

    [HttpGet("Versions/{versionId:long}/File")]
    public async Task<IActionResult> OpenPdf(
        long versionId,
        long? shareId,
        CancellationToken ct)
    {
        // OpenFileAsync kiểm tra quyền trước khi mở file.
        var download = await pdfs.OpenFileAsync(versionId, shareId, ct);
        return File(download.Content, "application/pdf", enableRangeProcessing: true);
    }

    [HttpPost("UploadPdf")]
    [RequestSizeLimit(27L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27L * 1024 * 1024)]
    public async Task<IActionResult> UploadPdf(
        IFormFile? file,
        string? title,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return IndexError("Hãy chọn một file PDF.");
        }

        if (file.Length > MaxPdfBytes)
        {
            return IndexError("PDF tối đa 25 MiB.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var pdf = await pdfs.UploadAsync(stream, file.FileName, title, ct);
            return RedirectToAction(nameof(Reader), new { id = pdf.DocumentId });
        }
        catch (NotesV3Exception ex)
        {
            return IndexError(ex.Message);
        }
    }

    [HttpPost("RenamePdf")]
    public async Task<IActionResult> RenamePdf(
        long id,
        string? title,
        string? versionToken,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
        {
            return IndexError("Tên PDF phải có từ 1 đến 200 ký tự.");
        }

        try
        {
            await pdfs.RenameAsync(id, title.Trim(), versionToken!, ct);
            TempData["Success"] = "Đã đổi tên PDF.";
            return RedirectToAction("Index", "NotesV3");
        }
        catch (NotesV3Exception ex)
        {
            return IndexError(ex.Message);
        }
    }

    [HttpPost("SetPdfArchived")]
    public async Task<IActionResult> SetPdfArchived(
        long id,
        bool archived,
        string? versionToken,
        CancellationToken ct)
    {
        try
        {
            await pdfs.SetArchivedAsync(id, archived, versionToken!, ct);
            TempData["Success"] = archived ? "Đã lưu trữ PDF." : "Đã bỏ lưu trữ PDF.";
            return RedirectToAction("Index", "NotesV3");
        }
        catch (NotesV3Exception ex)
        {
            return IndexError(ex.Message);
        }
    }

    [HttpPost("SharePdf")]
    public async Task<IActionResult> SharePdf(
        long documentId,
        long groupId,
        CancellationToken ct)
    {
        try
        {
            await sharing.SharePdfAsync(documentId, groupId, ct);
            TempData["Success"] = "Đã chia sẻ PDF vào nhóm.";
            return RedirectToAction("Index", "NotesV3");
        }
        catch (NotesV3Exception ex)
        {
            return IndexError(ex.Message);
        }
    }

    [HttpPost("DeletePdf")]
    public async Task<IActionResult> DeletePdf(
        long id,
        string? versionToken,
        CancellationToken ct)
    {
        try
        {
            await pdfs.DeleteAsync(id, versionToken!, ct);
            TempData["Success"] = "Đã xóa PDF.";
            return RedirectToAction("Index", "NotesV3");
        }
        catch (NotesV3Exception ex)
        {
            return IndexError(ex.Message);
        }
    }

    [HttpPost("RestorePdf")]
    public async Task<IActionResult> RestorePdf(
        long id,
        string? versionToken,
        CancellationToken ct)
    {
        try
        {
            await pdfs.RestoreAsync(id, versionToken!, ct);
            TempData["Success"] = "Đã khôi phục PDF.";
        }
        catch (NotesV3Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Trash", "NotesV3");
    }

    [HttpPost("SavePdfPageNote")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> SavePdfPageNote(
        long documentId,
        long versionId,
        int pageNumber,
        string? text,
        string? versionToken,
        long? shareId,
        CancellationToken ct)
    {
        try
        {
            await pageNotes.SaveTextAsync(
                versionId,
                pageNumber,
                text ?? string.Empty,
                versionToken,
                shareId,
                ct);

            TempData["Success"] = "Đã lưu ghi chú trang PDF.";
            return RedirectToAction(nameof(Reader), new
            {
                id = documentId,
                page = pageNumber,
                shareId
            });
        }
        catch (NotesV3Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Reader), new
            {
                id = documentId,
                page = pageNumber,
                shareId
            });
        }
    }

    private IActionResult IndexError(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction("Index", "NotesV3");
    }
}
