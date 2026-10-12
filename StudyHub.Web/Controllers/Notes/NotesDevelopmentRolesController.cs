using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.Web.Services.Notes;
using StudyHub.Web.ViewModels.Notes;

namespace StudyHub.Web.Controllers.Notes;

[AllowAnonymous]
[Route("Notes/DevelopmentRoles")]
public sealed class NotesDevelopmentRolesController(
    GroupNotesService notes,
    IWebHostEnvironment environment) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? error = null, CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (User.Identity?.IsAuthenticated != true) return Challenge();

        var personas = await notes.GetDevelopmentPersonasAsync(cancellationToken);
        var message = error == "persona_unavailable"
            ? "Persona vừa chọn không còn hợp lệ. Tải lại trang."
            : error == "group_create_failed"
                ? "Không tạo được nhóm kiểm thử. Kiểm tra trạng thái tài khoản và cấu hình database."
                : null;

        return View("~/Views/Notes/DevelopmentRoles.cshtml",
            new NotesDevelopmentRolesViewModel(personas, message,
                personas.Count == 0 && await notes.CanCreateDevelopmentTestGroupAsync(cancellationToken)));
    }

    [HttpPost("CreateSimulationGroup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSimulationGroup(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (User.Identity?.IsAuthenticated != true) return Challenge();

        try
        {
            await notes.CreateDevelopmentTestGroupAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is GroupNotesException or StudyGroupException)
        {
            return RedirectToAction(nameof(Index), new { error = "group_create_failed" });
        }
    }

    [HttpPost("Switch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Switch(
        string personaKey, long userId, long groupId, CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (User.Identity?.IsAuthenticated != true) return Challenge();

        var persona = await notes.FindDevelopmentPersonaAsync(personaKey, userId, groupId, cancellationToken);
        if (persona is null)
            return RedirectToAction(nameof(Index), new { error = "persona_unavailable" });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, persona.UserId.ToString()),
            new(ClaimTypes.Name, persona.IsSimulated
                ? $"{persona.DisplayName} · Member giả lập"
                : persona.DisplayName),
            new(ClaimTypes.Role, persona.RoleCode),
            new("login_provider", "NotesDevelopmentPersona")
        };

        if (persona.IsSimulated)
        {
            claims.Add(new Claim("notes_dev_persona", "member-simulation"));
            claims.Add(new Claim("notes_dev_group", persona.GroupId.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(persona.Email))
            claims.Add(new Claim(ClaimTypes.Email, persona.Email));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        return Redirect($"/Notes/Groups/{persona.GroupId}");
    }
}
