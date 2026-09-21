using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Dashboard;

namespace StudyHub.Web.Controllers.Dashboard;

public sealed class DashboardController : Controller
{
    [HttpGet("/")]
    [HttpGet("/Dashboard")]
    public IActionResult Index() => View(new DashboardIndexViewModel("Nguyễn Minh Anh", 12));
}
