using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.BLL.Services.NotesV3.Annotations;

namespace StudyHub.Web.Controllers.NotesV3;

[Authorize]
[AutoValidateAntiforgeryToken]
[Route("NotesV3/PdfMarks")]
public sealed class PdfMarksController(IPdfMarksService marks) : Controller
{
    [HttpPost("Save")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Save(
        long documentId, long versionId, int pageNumber, long? shareId,
        string? marksJson,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(marksJson) || marksJson.Length > 3_000_000)
            {
                throw new NotesV3Exception(400, "Dữ liệu đánh dấu không hợp lệ.");
            }

            var inputs = JsonSerializer.Deserialize<List<PdfMarkInput>>(
                marksJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new NotesV3Exception(400, "Dữ liệu đánh dấu không hợp lệ.");

            await marks.CreateManyAsync(versionId, pageNumber, shareId, inputs, ct);
            TempData["Success"] = $"Đã lưu {inputs.Count} dấu trên PDF.";
        }
        catch (JsonException)
        {
            TempData["Error"] = "Nét vẽ không hợp lệ.";
        }
        catch (NotesV3Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Viewer", "Pdfs",
            new { id = documentId, page = pageNumber, shareId });
    }

    [HttpPost("Delete")]
    public async Task<IActionResult> Delete(
        long markId, long documentId, long versionId, int pageNumber,
        long? shareId, string versionToken, CancellationToken ct)
    {
        try
        {
            await marks.DeleteAsync(markId, versionId, pageNumber,
                shareId, versionToken, ct);
            TempData["Success"] = "Đã xóa đánh dấu.";
        }
        catch (NotesV3Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Viewer", "Pdfs",
            new { id = documentId, page = pageNumber, shareId });
    }
}
