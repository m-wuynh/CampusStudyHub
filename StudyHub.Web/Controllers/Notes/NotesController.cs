using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Notes;

namespace StudyHub.Web.Controllers.Notes;

[Route("Notes")]
public sealed class NotesController : Controller
{
    [HttpGet("")]
    public IActionResult Index([FromQuery] NotesIndexViewModel model) => View(model);
}
