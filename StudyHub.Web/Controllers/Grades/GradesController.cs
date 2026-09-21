using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Grades;

namespace StudyHub.Web.Controllers.Grades;

[Route("Grades")]
public sealed class GradesController : Controller
{
    [HttpGet("")]
    public IActionResult Index(long? academicTermId) => View(new GradesIndexViewModel(academicTermId));
}
