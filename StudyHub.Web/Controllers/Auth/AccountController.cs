using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Auth;

namespace StudyHub.Web.Controllers.Auth;

[Route("Account")]
public sealed class AccountController : Controller
{
    [HttpGet("Login")]
    [HttpGet("/Login")]
    public IActionResult Login() => View("~/Views/Auth/Login.cshtml", new LoginViewModel());

    [ValidateAntiForgeryToken]
    [HttpPost("Login")]
    public IActionResult Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View("~/Views/Auth/Login.cshtml", model);
        return RedirectToAction("Index", "Dashboard");
    }

    [ValidateAntiForgeryToken]
    [HttpPost("Register")]
    public IActionResult Register() => RedirectToAction(nameof(Login));
}
