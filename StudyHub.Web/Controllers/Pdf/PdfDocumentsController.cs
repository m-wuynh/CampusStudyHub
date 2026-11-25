using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Ai;
using StudyHub.BLL.Services.Pdf.Documents;

namespace StudyHub.Web.Controllers.Pdf;

[Authorize]
[AutoValidateAntiforgeryToken]
[Route("Pdf/Documents")]
public sealed class PdfDocumentsController(
    PdfDocumentService documentService,
    PdfAiService aiService) : Controller
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Reader(
        long id,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var reader = await documentService.GetReaderAsync(
            id,
            page,
            cancellationToken);

        return View("~/Views/Pdf/Reader.cshtml", reader);
    }

    [HttpGet("{id:long}/File")]
    public async Task<IActionResult> File(
        long id,
        CancellationToken cancellationToken)
    {
        var stream = await documentService.OpenAsync(id, cancellationToken);

        return File(
            stream,
            "application/pdf",
            enableRangeProcessing: true);
    }

    [HttpPost("{id:long}/Note")]
    public async Task<IActionResult> SaveNote(
        long id,
        int page,
        string? content,
        CancellationToken cancellationToken)
    {
        await documentService.SaveNoteAsync(
            id,
            page,
            content,
            cancellationToken);

        TempData["Success"] = string.IsNullOrWhiteSpace(content)
            ? $"Đã xóa ghi chú trang {page}."
            : $"Đã lưu ghi chú trang {page}.";
        return RedirectToAction(nameof(Reader), new { id, page });
    }

    [HttpPost("{id:long}/Delete")]
    public async Task<IActionResult> Delete(
        long id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var subjectId = await documentService.DeleteAsync(
            id,
            cancellationToken);

        TempData["Success"] =
            $"Đã chuyển PDF vào thùng rác trong " +
            $"{PdfPolicy.TrashRetentionDays} ngày.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Folder", "Pdf", new { subjectId });
    }

    [HttpPost("{id:long}/Rename")]
    public async Task<IActionResult> Rename(
        long id,
        string? title,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        await documentService.RenameAsync(id, title, cancellationToken);
        TempData["Success"] = "Đã đổi tên PDF.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Reader), new { id });
    }

    [HttpPost("{id:long}/Restore")]
    public async Task<IActionResult> Restore(
        long id,
        CancellationToken cancellationToken)
    {
        await documentService.RestoreAsync(id, cancellationToken);

        TempData["Success"] = "Đã khôi phục PDF.";
        return RedirectToAction("Trash", "Pdf");
    }

    [HttpPost("{id:long}/Analyze")]
    public async Task<IActionResult> Analyze(
        long id,
        int fromPage,
        int toPage,
        CancellationToken cancellationToken)
    {
        try
        {
            var analysisId = await aiService.AnalyzeAsync(
                id,
                fromPage,
                toPage,
                cancellationToken);

            return RedirectToAction(nameof(Review), new { id = analysisId });
        }
        catch (PdfException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(
                nameof(Reader),
                new { id, page = fromPage });
        }
    }

    [HttpGet("Analysis/{id:long}")]
    public async Task<IActionResult> Review(
        long id,
        CancellationToken cancellationToken)
    {
        var analysis = await aiService.GetAsync(id, cancellationToken);
        return View("~/Views/Pdf/Review.cshtml", analysis);
    }

    [HttpPost("Analysis/{id:long}/Accept")]
    public async Task<IActionResult> Accept(
        long id,
        AcceptPdfAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.HasMatchingConceptFields())
        {
            return BadRequest();
        }

        var analysis = await aiService.GetAsync(id, cancellationToken);
        await aiService.AcceptAsync(
            id,
            request.ToResult(),
            cancellationToken);

        TempData["Success"] =
            "Đã lưu toàn bộ kết quả AI vào ghi chú trang nguồn.";

        return RedirectToAction(
            nameof(Reader),
            new
            {
                id = analysis.DocumentId,
                page = analysis.FromPage
            });
    }

    [HttpPost("Analysis/{id:long}/Discard")]
    public async Task<IActionResult> Discard(
        long id,
        CancellationToken cancellationToken)
    {
        var analysis = await aiService.GetAsync(id, cancellationToken);
        await aiService.DiscardAsync(id, cancellationToken);

        return RedirectToAction(
            nameof(Reader),
            new
            {
                id = analysis.DocumentId,
                page = analysis.FromPage
            });
    }
}

public sealed class AcceptPdfAnalysisRequest
{
    public string Summary { get; init; } = string.Empty;

    public List<string> KeyPoints { get; init; } = [];

    public List<string> ConceptTitles { get; init; } = [];

    public List<string> ConceptSummaries { get; init; } = [];

    public List<string> Questions { get; init; } = [];

    public List<string> Answers { get; init; } = [];

    public bool HasMatchingConceptFields()
    {
        var count = ConceptTitles.Count;

        return ConceptSummaries.Count == count &&
               Questions.Count == count &&
               Answers.Count == count;
    }

    public PdfAiResult ToResult()
    {
        var concepts = Enumerable.Range(0, ConceptTitles.Count)
            .Select(index => new PdfAiConcept(
                ConceptTitles[index],
                ConceptSummaries[index],
                Questions[index],
                Answers[index]))
            .ToList();

        var keyPoints = KeyPoints
            .Where(point => !string.IsNullOrWhiteSpace(point))
            .ToList();

        return new PdfAiResult(Summary, keyPoints, concepts);
    }
}
