using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.BLL.Services.NotesV3.Dashboard;
using StudyHub.BLL.Services.NotesV3.Notebooks;
using StudyHub.BLL.Services.NotesV3.PageNotes;
using StudyHub.BLL.Services.NotesV3.Pdfs;
using StudyHub.BLL.Services.NotesV3.Sharing;
using StudyHub.Web.Filters;
using StudyHub.Web.ViewModels.NotesV3;

namespace StudyHub.Web.Controllers.NotesV3;

[Authorize]
[Route("NotesV3")]
[ServiceFilter(typeof(NotesV3ExceptionFilter))]
public sealed class NotesV3Controller(
    INotesDashboardService dashboard,
    INotebookService notebooks,
    IPdfService pdfs,
    IPageNotesService pageNotes,
    IGroupShareService sharing) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var counts = await dashboard.GetAsync(ct);

        var personalNotebooks = await notebooks.ListAsync(
            groupId: null,
            search: null,
            ct: ct);

        var personalPdfs = await pdfs.ListAsync(
            includeArchived: true,
            ct: ct);

        var groups = await sharing.ListEligibleGroupsAsync(ct);

        return View(new NotesIndexViewModel(
            counts,
            personalNotebooks,
            personalPdfs,
            groups));
    }

    [HttpGet("Notebook/{id:long}")]
public async Task<IActionResult> Notebook(
    long id,
    CancellationToken ct)
{
    var details = await notebooks.GetAsync(id, ct);

    var availableNotebooks = await notebooks.ListAsync(
        details.Notebook.GroupId,
        search: null,
        ct: ct);

    var groups = await sharing.ListEligibleGroupsAsync(ct);

    return View(new NotesNotebookViewModel(
        details,
        groups,
        availableNotebooks));
}

    [HttpGet("Pdf/{id:long}")]
    public async Task<IActionResult> Reader(
        long id,
        long? shareId,
        int page = 1,
        CancellationToken ct = default)
    {
        var pdf = await pdfs.GetAsync(id, shareId, ct);

        if (page < 1 ||
            (pdf.PageCount is int count && page > count))
        {
            throw new NotesV3Exception(
                400,
                "Trang nằm ngoài tài liệu PDF.");
        }

        return View(new NotesReaderViewModel(
            pdf,
            shareId,
            page));
    }

    [HttpGet("Book/{id:long}")]
    public async Task<IActionResult> Book(
        long id,
        long? shareId,
        CancellationToken ct)
    {
        var pdf = await pdfs.GetAsync(id, shareId, ct);

        var pages = await pageNotes.ListBookPagesAsync(
            pdf.DocumentVersionId,
            shareId,
            ct);

        return View(new NotesBookViewModel(
            pdf,
            shareId,
            pages));
    }
}