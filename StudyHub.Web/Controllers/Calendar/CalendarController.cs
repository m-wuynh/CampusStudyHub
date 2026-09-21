using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Calendar;

namespace StudyHub.Web.Controllers.Calendar;

[Route("Calendar")]
public sealed class CalendarController : Controller
{
    [HttpGet("")]
    public IActionResult Index(DateOnly? date) =>
        View(new CalendarIndexViewModel(date ?? DateOnly.FromDateTime(DateTime.Today)));
}
