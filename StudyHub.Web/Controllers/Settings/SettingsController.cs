using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Settings;

namespace StudyHub.Web.Controllers.Settings;

[Route("Settings")]
public sealed class SettingsController : Controller
{
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpGet("")]
    public IActionResult Index() => View(new SettingsIndexViewModel());
}
