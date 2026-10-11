using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Notes;

namespace StudyHub.Web.Controllers.Notes;

[Route("Notes")]
public sealed class NotesController(IWebHostEnvironment environment) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["ShowNotesDevelopmentRoles"] = environment.IsDevelopment();
        return View("~/Views/Notes/Index.cshtml");
    }

    [HttpGet("Reader/{id:long}")]
    public IActionResult Reader(long id) => View("~/Views/Notes/Reader.cshtml", id);

    [HttpGet("Note/{id:long}")]
    public IActionResult Note(long id) => View("~/Views/Notes/Note.cshtml", id);

    [HttpGet("Groups/{groupId:long}")]
    public IActionResult GroupWorkspace(long groupId) => View("~/Views/Notes/GroupWorkspace.cshtml", groupId);

    [HttpGet("Groups/{groupId:long}/Documents/{documentId:long}")]
    public IActionResult GroupReader(long groupId, long documentId) =>
        View("~/Views/Notes/GroupReader.cshtml", new GroupPdfReaderModel(groupId, documentId));
}
