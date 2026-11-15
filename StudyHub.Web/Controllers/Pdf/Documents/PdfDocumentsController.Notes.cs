using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyHub.BLL.Services.Pdf;
using StudyHub.Web.Configuration;

namespace StudyHub.Web.Controllers.Pdf.Documents;
public sealed partial class PdfDocumentsController
{
    [HttpGet("{documentId:long}/PageState")]
    public async Task<IActionResult> PageState(
        long documentId,
        int pageNumber,
        long? shareId,
        CancellationToken ct)
    {
        var pdf = await pdfService.GetAsync(documentId, shareId, ct);
        if (pageNumber < 1 || pageNumber > pdf.PageCount)
        {
            throw new PdfException(400, "Trang PDF không hợp lệ.");
        }

        var noteText = string.Empty;
        string? noteVersionToken = null;
        if (!shareId.HasValue)
        {
            var note = await pdfPageNotes.GetTextAsync(
                pdf.DocumentId, pageNumber, null, ct);
            noteText = note.Text;
            noteVersionToken = note.VersionToken;
        }

        return Json(new
        {
            documentId = pdf.DocumentId,
            pageNumber,
            noteText,
            noteVersionToken
        });
    }

    [HttpPost("AutoSavePdfPageNote")]
    [EnableRateLimiting(PdfRateLimitPolicies.Writes)]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> AutoSavePdfPageNote(
        long documentId,
        int pageNumber,
        string? text,
        string? versionToken,
        long? shareId,
        CancellationToken ct)
    {
        if (shareId.HasValue)
        {
            throw new PdfException(403, "PDF chia sẻ trong nhóm học chỉ có thể xem.");
        }

        var nextVersion = await pdfPageNotes.SaveTextAsync(
            documentId,
            pageNumber,
            text ?? string.Empty,
            versionToken,
            shareId,
            ct);

        return Json(new { versionToken = nextVersion });
    }
}
