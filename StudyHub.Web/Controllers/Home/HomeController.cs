using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Home;

namespace StudyHub.Web.Controllers.Home;

public sealed class HomeController : Controller
{
    [HttpGet("/Privacy")]
    public IActionResult Privacy() => View();

    [HttpGet("/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));
}
