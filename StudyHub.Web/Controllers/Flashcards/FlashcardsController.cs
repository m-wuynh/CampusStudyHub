using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Flashcards;

namespace StudyHub.Web.Controllers.Flashcards;

[Route("Flashcards")]
public sealed class FlashcardsController : Controller
{
    [HttpGet("")]
    public IActionResult Index([FromQuery] FlashcardsIndexViewModel model) => View(model);
}
