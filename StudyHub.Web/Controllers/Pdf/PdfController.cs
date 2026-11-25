using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Documents;

namespace StudyHub.Web.Controllers.Pdf;

[Authorize]
[Route("Pdf")]
public sealed class PdfController(
    PdfLibraryService libraryService,
    PdfDocumentService documentService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var library = await libraryService.GetAsync(cancellationToken);
        return View("~/Views/Pdf/Index.cshtml", library);
    }

    [HttpGet("Folder")]
    public async Task<IActionResult> Folder(
        long? subjectId,
        CancellationToken cancellationToken)
    {
        var folder = await libraryService.GetFolderAsync(
            subjectId,
            cancellationToken);

        return View("~/Views/Pdf/Folder.cshtml", folder);
    }

    [HttpGet("Upload")]
    public async Task<IActionResult> Upload(
        long? subjectId,
        CancellationToken cancellationToken)
    {
        ViewBag.SubjectId = subjectId;

        var library = await libraryService.GetAsync(cancellationToken);
        return View("~/Views/Pdf/Upload.cshtml", library);
    }

    [HttpPost("Upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(PdfPolicy.MaxUploadRequestBytes)]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        string? title,
        long? subjectId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new PdfException(400, "Hãy chọn một file PDF.");
        }

        if (file.Length > PdfPolicy.MaxFileBytes)
        {
            throw new PdfException(413, "PDF tối đa 25 MiB.");
        }

        await using var stream = file.OpenReadStream();
        var documentId = await documentService.UploadAsync(
            stream,
            file.FileName,
            title,
            subjectId,
            cancellationToken);

        TempData["Success"] = "Đã tải PDF lên.";

        return RedirectToAction(
            "Reader",
            "Pdf",
            new { id = documentId });
    }

    [HttpGet("Trash")]
    public async Task<IActionResult> Trash(
        CancellationToken cancellationToken)
    {
        var documents = await libraryService.GetTrashAsync(cancellationToken);
        return View("~/Views/Pdf/Trash.cshtml", documents);
    }

    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard(
        long? subjectId,
        CancellationToken cancellationToken)
    {
        var dashboard = await libraryService.GetDashboardAsync(
            subjectId,
            cancellationToken);

        return View("~/Views/Pdf/Dashboard.cshtml", dashboard);
    }
}
