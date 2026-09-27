using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Auth;
using StudyHub.Web.ViewModels.Auth;

namespace StudyHub.Web.Controllers.Auth;

[Route("Account")]
public sealed class AccountController(
    IAuthService authService,
    IAuthenticationSchemeProvider schemeProvider,
    ILogger<AccountController> logger) : Controller
{
    private const string LoginView = "~/Views/Auth/Login.cshtml";
    private const string PictureClaimType = "urn:google:picture";

    [AllowAnonymous]
    [HttpGet("Login")]
    [HttpGet("/Login")]
    public async Task<IActionResult> Login(string? returnUrl = null, string? error = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirectOrDashboard(returnUrl);

        var model = await CreateLoginViewModelAsync(returnUrl, GetLoginError(error));
        return View(LoginView, model);
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("Login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ModelState.AddModelError(string.Empty, "Đăng nhập bằng mật khẩu chưa được cấu hình. Hãy dùng nút Đăng nhập với Google.");
        model.GoogleEnabled = await IsGoogleEnabledAsync();
        return View(LoginView, model);
    }

    [AllowAnonymous]
    [HttpGet("Google")]
    public async Task<IActionResult> Google(string? returnUrl = null, bool rememberMe = true)
    {
        if (!await IsGoogleEnabledAsync())
            return RedirectToAction(nameof(Login), new { returnUrl, error = "google_not_configured" });

        var callbackUrl = Url.Action(
            nameof(GoogleCallback),
            "Account",
            new { returnUrl = SafeReturnUrl(returnUrl), rememberMe });

        return Challenge(
            new AuthenticationProperties { RedirectUri = callbackUrl },
            GoogleDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("GoogleCallback")]
    public async Task<IActionResult> GoogleCallback(
        string? returnUrl = null,
        bool rememberMe = true,
        CancellationToken cancellationToken = default)
    {
        var googleSubject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var displayName = User.FindFirstValue(ClaimTypes.Name);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var avatarUrl = User.FindFirstValue(PictureClaimType);

        if (string.IsNullOrWhiteSpace(googleSubject) || string.IsNullOrWhiteSpace(displayName))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login), new { returnUrl, error = "google_claims" });
        }

        try
        {
            var result = await authService.LoginWithGoogleAsync(
                new GoogleLoginDto(googleSubject, displayName, email, avatarUrl),
                cancellationToken);

            if (!result.Succeeded || result.User is null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                var model = await CreateLoginViewModelAsync(returnUrl, result.ErrorMessage);
                return View(LoginView, model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                new(ClaimTypes.Name, result.User.DisplayName),
                new(ClaimTypes.Role, result.User.RoleCode),
                new("google_sub", googleSubject)
            };

            if (!string.IsNullOrWhiteSpace(result.User.Email))
                claims.Add(new Claim(ClaimTypes.Email, result.User.Email));
            if (!string.IsNullOrWhiteSpace(avatarUrl))
                claims.Add(new Claim(PictureClaimType, avatarUrl));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var properties = new AuthenticationProperties { IsPersistent = rememberMe };
            if (rememberMe)
                properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                properties);

            return LocalRedirectOrDashboard(returnUrl);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Không thể hoàn tất đăng nhập Google cho subject {GoogleSubject}.", googleSubject);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login), new { returnUrl, error = "google_storage" });
        }
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("Register")]
    public IActionResult Register() =>
        RedirectToAction(nameof(Login), new { error = "use_google" });

    private async Task<LoginViewModel> CreateLoginViewModelAsync(string? returnUrl, string? errorMessage) => new()
    {
        ReturnUrl = SafeReturnUrl(returnUrl),
        RememberMe = true,
        GoogleEnabled = await IsGoogleEnabledAsync(),
        ErrorMessage = errorMessage
    };

    private async Task<bool> IsGoogleEnabledAsync() =>
        await schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is not null;

    private string? SafeReturnUrl(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    private IActionResult LocalRedirectOrDashboard(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");

    private static string? GetLoginError(string? error) => error switch
    {
        "google_not_configured" => "Đăng nhập Google chưa được cấu hình trên máy này.",
        "google_failed" => "Google đã hủy hoặc không thể hoàn tất đăng nhập. Vui lòng thử lại.",
        "google_claims" => "Không đọc được thông tin tài khoản từ Google.",
        "google_storage" => "Đã đăng nhập Google nhưng không thể lưu tài khoản vào cơ sở dữ liệu.",
        "use_google" => "Tài khoản được tạo tự động ở lần đầu bạn đăng nhập với Google.",
        _ => null
    };
}
