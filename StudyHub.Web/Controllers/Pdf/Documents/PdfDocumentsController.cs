using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Notes;
using StudyHub.BLL.Services.Pdf.Annotations;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.Web.ViewModels.Pdf;

namespace StudyHub.Web.Controllers.Pdf.Documents;

[Authorize]
[AutoValidateAntiforgeryToken]
[Route("Pdf/Documents")]
public sealed partial class PdfDocumentsController(
    PdfService pdfs,
    PdfPageNotesService pageNotes,
    PdfMarksService marks) : Controller
{
    private readonly PdfService pdfService = pdfs;
    private readonly PdfPageNotesService pdfPageNotes = pageNotes;
    private readonly PdfMarksService pdfMarks = marks;

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Reader(
        long id,
        int? page = null,
        long? shareId = null,
        CancellationToken ct = default)
    {
        var model = await GetReaderModelAsync(
            id, page, shareId, includeReaderState: true, ct);
        return View("~/Views/Pdf/Reader.cshtml", model);
    }

    [HttpGet("{id:long}/Viewer")]
    public async Task<IActionResult> Viewer(
        long id,
        int? page = null,
        long? shareId = null,
        CancellationToken ct = default)
    {
        var model = await GetReaderModelAsync(
            id, page, shareId, includeReaderState: false, ct);
        return View("~/Views/Pdf/PdfViewer.cshtml", model);
    }

    private async Task<PdfReaderViewModel> GetReaderModelAsync(
        long id,
        int? requestedPage,
        long? shareId,
        bool includeReaderState,
        CancellationToken ct)
    {
        var pdf = await pdfService.GetAsync(id, shareId, ct);
        var pageCount = pdf.PageCount;

        var page = requestedPage ?? 1;
        if (page < 1 || page > pageCount)
        {
            throw new PdfException(400, "Trang nằm ngoài tài liệu PDF.");
        }

        var noteText = string.Empty;
        string? noteVersionToken = null;
        IReadOnlyList<PdfMarkViewModel> markModels = [];

        if (includeReaderState)
        {
            if (!shareId.HasValue)
            {
                var note = await pdfPageNotes.GetTextAsync(
                    pdf.DocumentId,
                    page,
                    null,
                    ct);
                noteText = note.Text;
                noteVersionToken = note.VersionToken;
            }

        }
        else
        {
            var pageMarks = await pdfMarks.ListAsync(
                pdf.DocumentId, page, shareId, ct);
            markModels = pageMarks.Select(item => new PdfMarkViewModel(
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
                item.Mark.Text)).ToList();
        }

        var model = new PdfReaderViewModel(
            pdf.DocumentId,
            pdf.Title,
            pdf.FileName,
            pageCount,
            page,
            noteText,
            noteVersionToken,
            shareId,
            pdf.GroupId,
            markModels);

        return model;
    }

}
