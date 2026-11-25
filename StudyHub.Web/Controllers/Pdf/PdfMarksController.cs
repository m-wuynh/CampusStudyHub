using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Annotations;
using StudyHub.Web.Configuration;

namespace StudyHub.Web.Controllers.Pdf;

[Authorize]
[AutoValidateAntiforgeryToken]
[Route("Pdf/Marks")]
public sealed class PdfMarksController(PdfMarksService marks) : Controller
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [HttpPost("AutoSave")]
    [EnableRateLimiting(PdfRateLimitPolicies.Writes)]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> AutoSave(
        long documentId, int pageNumber, long? shareId,
        string? marksJson,
        CancellationToken ct)
    {
        try
        {
            var inputs = ParseInputs(marksJson, shareId);

            var saved = await marks.CreateManyAsync(
                documentId, pageNumber, shareId, inputs, ct);
            return Json(new
            {
                marks = saved.Select(mark => new
                {
                    id = mark.Id,
                    versionToken = mark.VersionToken
                })
            });
        }
        catch (PdfException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("Delete")]
    [EnableRateLimiting(PdfRateLimitPolicies.Writes)]
    public async Task<IActionResult> Delete(
        long markId, long documentId, int pageNumber,
        long? shareId, string versionToken, CancellationToken ct)
    {
        try
        {
            RequirePersonalPdf(shareId);
            await marks.DeleteAsync(markId, documentId, pageNumber,
                shareId, versionToken, ct);
            TempData["Success"] = "Đã xóa đánh dấu.";
        }
        catch (PdfException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Viewer", "PdfDocuments",
            new { id = documentId, page = pageNumber, shareId });
    }

    private static List<PdfMarkInput> ParseInputs(
        string? marksJson,
        long? shareId)
    {
        RequirePersonalPdf(shareId);
        if (string.IsNullOrWhiteSpace(marksJson) || marksJson.Length > 3_000_000)
        {
            throw new PdfException(400, "Dữ liệu đánh dấu không hợp lệ.");
        }

        try
        {
            return JsonSerializer.Deserialize<List<PdfMarkInput>>(
                marksJson, JsonOptions)
                ?? throw new PdfException(400, "Dữ liệu đánh dấu không hợp lệ.");
        }
        catch (JsonException)
        {
            throw new PdfException(400, "Nét vẽ không hợp lệ.");
        }
    }

    private static void RequirePersonalPdf(long? shareId)
    {
        if (shareId.HasValue)
        {
            throw new PdfException(
                403,
                "PDF chia sẻ trong nhóm học chỉ có thể xem, không thể chỉnh sửa.");
        }
    }
}
