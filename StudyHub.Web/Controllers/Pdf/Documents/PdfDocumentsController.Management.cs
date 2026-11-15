using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.Web.Configuration;

namespace StudyHub.Web.Controllers.Pdf.Documents;

public sealed partial class PdfDocumentsController
{
    [HttpGet("{documentId:long}/File")]
    public async Task<IActionResult> OpenPdf(
        long documentId,
        long? shareId,
        CancellationToken ct)
    {
        var content = await pdfService.OpenFileAsync(documentId, shareId, ct);
        return File(content, "application/pdf", enableRangeProcessing: true);
    }

    [HttpPost("UploadPdf")]
    [EnableRateLimiting(PdfRateLimitPolicies.Uploads)]
    [RequestSizeLimit(PdfPolicy.MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PdfPolicy.MaxUploadRequestBytes)]
    public async Task<IActionResult> UploadPdf(
        IFormFile? file,
        string? title,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return IndexError("Hãy chọn một file PDF.");
        }

        if (file.Length > PdfPolicy.MaxFileBytes)
        {
            return IndexError("PDF tối đa 25 MiB.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var pdf = await pdfService.UploadAsync(stream, file.FileName, title, ct);
            return RedirectToAction(nameof(Reader), new { id = pdf.DocumentId });
        }
        catch (PdfException ex)
        {
            return IndexError(ex.Message);
        }
    }

    [HttpPost("RenamePdf")]
    [EnableRateLimiting(PdfRateLimitPolicies.Writes)]
    public async Task<IActionResult> RenamePdf(
        long id,
        string? title,
        string? versionToken,
        CancellationToken ct)
    {
        return await RunIndexCommandAsync(
            () => pdfService.RenameAsync(id, title!, versionToken!, ct),
            "Đã đổi tên PDF.");
    }

    [HttpPost("DeletePdf")]
    [EnableRateLimiting(PdfRateLimitPolicies.Writes)]
    public async Task<IActionResult> DeletePdf(
        long id,
        string? versionToken,
        CancellationToken ct)
    {
        return await RunIndexCommandAsync(
            () => pdfService.DeleteAsync(id, versionToken!, ct),
            "Đã xóa PDF.");
    }

    [HttpPost("RestorePdf")]
    [EnableRateLimiting(PdfRateLimitPolicies.Writes)]
    public async Task<IActionResult> RestorePdf(
        long id,
        string? versionToken,
        CancellationToken ct)
    {
        return await RunTrashCommandAsync(
            () => pdfService.RestoreAsync(id, versionToken!, ct),
            "Đã khôi phục PDF.");
    }

    [HttpPost("PermanentlyDeletePdf")]
    [EnableRateLimiting(PdfRateLimitPolicies.Writes)]
    public async Task<IActionResult> PermanentlyDeletePdf(
        long id,
        string? versionToken,
        CancellationToken ct)
    {
        return await RunTrashCommandAsync(
            () => pdfService.PermanentlyDeleteAsync(id, versionToken!, ct),
            "Đã xóa vĩnh viễn PDF.");
    }

    private async Task<IActionResult> RunIndexCommandAsync(
        Func<Task> command,
        string successMessage)
    {
        try
        {
            await command();
            TempData["Success"] = successMessage;
            return RedirectToAction("Index", "Pdf");
        }
        catch (PdfException ex)
        {
            return IndexError(ex.Message);
        }
    }

    private async Task<IActionResult> RunTrashCommandAsync(
        Func<Task> command,
        string successMessage)
    {
        try
        {
            await command();
            TempData["Success"] = successMessage;
        }
        catch (PdfException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Trash", "Pdf");
    }

    private IActionResult IndexError(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction("Index", "Pdf");
    }
}
