using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.Pdf;
using StudyHub.Web.ViewModels.Pdf;

namespace StudyHub.Web.Controllers.Pdf;

[Authorize]
[Route("Pdf")]
public sealed class PdfController(
    PdfLibraryService indexService) : Controller
{
    [HttpGet("Trash")]
    public async Task<IActionResult> Trash(CancellationToken ct)
    {
        var data = await indexService.GetTrashAsync(ct);
        var model = new PdfTrashViewModel(
            data.Pdfs.Select(x => new TrashedPdfViewModel(
                x.Id, x.Title, x.FileName,
                x.VersionToken, x.DaysLeft)).ToList());

        return View("~/Views/Pdf/Trash.cshtml", model);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? q,
        int page = 1,
        CancellationToken ct = default)
    {
        var data = await indexService.GetAsync(q, page, ct);

        var model = new PdfLibraryViewModel(
            PdfCount: data.PdfCount,
            SearchQuery: data.SearchQuery,
            PageNumber: data.PageNumber,
            TotalPages: data.TotalPages,
            Pdfs: data.Pdfs
                .Select(item => new PdfCardViewModel(
                    item.Id,
                    item.Title,
                    item.FileName,
                    item.PageCount,
                    item.VersionToken,
                    item.SearchPage))
                .ToList());

        return View(model);
    }
}
