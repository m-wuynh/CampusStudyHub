using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.Web.ViewModels.NotesV3;

namespace StudyHub.Web.Controllers.NotesV3;

[Authorize]
[Route("NotesV3")]
public sealed class NotesV3Controller(
    INotesIndexService indexService) : Controller
{
    [HttpGet("Trash")]
    public async Task<IActionResult> Trash(CancellationToken ct)
    {
        var data = await indexService.GetTrashAsync(ct);
        var model = new NotesTrashViewModel(
            data.Notebooks.Select(x => new TrashedNotebookViewModel(
                x.Id, x.Title, x.DaysLeft)).ToList(),
            data.Pdfs.Select(x => new TrashedPdfViewModel(
                x.Id, x.Title, x.FileName,
                x.VersionToken, x.DaysLeft)).ToList());

        return View("~/Views/NotesV3/Trash.cshtml", model);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? groupId, CancellationToken ct)
    {
        var data = await indexService.GetAsync(groupId, ct);

        var model = new NotesIndexViewModel(
            NotebookCount: data.NotebookCount,
            PdfCount: data.PdfCount,
            SharedCount: data.SharedCount,
            Notebooks: data.Notebooks
                .Select(item => new NotebookCardViewModel(
                    item.Id,
                    item.Title))
                .ToList(),
            Pdfs: data.Pdfs
                .Select(item => new PdfCardViewModel(
                    item.Id,
                    item.Title,
                    item.FileName,
                    item.PageCount,
                    item.IsArchived,
                    item.VersionToken))
                .ToList(),
            Groups: data.Groups
                .Select(group => new GroupOptionViewModel(
                    group.Id,
                    group.Name))
                .ToList(),
            SelectedGroup: data.SelectedGroup is null
                ? null
                : new GroupOptionViewModel(
                    data.SelectedGroup.Id,
                    data.SelectedGroup.Name),
            GroupNotebooks: data.GroupNotebooks
                .Select(item => new NotebookCardViewModel(item.Id, item.Title))
                .ToList(),
            GroupPdfs: data.GroupPdfs
                .Select(item => new SharedPdfCardViewModel(
                    item.Id,
                    item.ShareId,
                    item.Title,
                    item.FileName,
                    item.PageCount))
                .ToList());

        return View(model);
    }
}
