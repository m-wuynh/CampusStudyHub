using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace StudyHub.Tests;

public sealed class AuthenticationAccessTests(AuthenticationAccessTests.AuthApplication app)
    : IClassFixture<AuthenticationAccessTests.AuthApplication>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Dashboard")]
    [InlineData("/Groups")]
    [InlineData("/Calendar")]
    [InlineData("/Flashcards")]
    [InlineData("/Grades")]
    [InlineData("/Settings")]
    [InlineData("/NotesV3")]
    [InlineData("/NotesV3/Trash")]
    [InlineData("/Pdf")]
    [InlineData("/Pdf/Trash")]
    [InlineData("/Pdf/Documents/1")]
    [InlineData("/Privacy")]
    [InlineData("/Error")]
    [InlineData("/Groups/1?panel=resources")]
    [InlineData("/hubs/study-groups")]
    public async Task Anonymous_page_requests_redirect_to_login(string path)
    {
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var destination = response.Headers.Location!.ToString();
        Assert.Contains("/Login?ReturnUrl=", destination);
        Assert.EndsWith(path, Uri.UnescapeDataString(destination));
    }

    [Theory]
    [InlineData("/Groups/Create")]
    [InlineData("/Calendar/Create")]
    [InlineData("/Flashcards/1/Learn")]
    [InlineData("/Calendar/GetEvents")]
    public async Task Feature_requires_login_and_preserves_destination(string path)
    {
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var destination = response.Headers.Location!.ToString();
        Assert.Contains("/Login?ReturnUrl=", destination);
        Assert.EndsWith(path, Uri.UnescapeDataString(destination));
    }

    [Theory]
    [InlineData("/Groups/Create")]
    [InlineData("/Flashcards/Create")]
    [InlineData("/Grades/CreateGradeBook")]
    [InlineData("/Calendar/CreateEvent")]
    [InlineData("/Pdf/Documents/UploadPdf")]
    [InlineData("/Account/Register")]
    [InlineData("/hubs/study-groups/negotiate?negotiateVersion=1")]
    public async Task Anonymous_users_cannot_post_mutations(string path)
    {
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsync(path, new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login?ReturnUrl=", response.Headers.Location!.ToString());
    }

    public sealed class AuthApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:StudyHub", "Server=localhost;Database=AuthAccessTests;Integrated Security=true;TrustServerCertificate=true");
            builder.UseSetting("Authentication:Google:ClientId", "test-client");
            builder.UseSetting("Authentication:Google:ClientSecret", "test-secret");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                var notificationService = services.FirstOrDefault(service =>
                    service.ServiceType == typeof(IHostedService) &&
                    service.ImplementationType == typeof(StudyHub.Web.BackgroundServices.EmailNotificationBackgroundService));
                if (notificationService is not null) services.Remove(notificationService);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        }
    }

    [Theory]
    [InlineData("/Login")]
    [InlineData("/Account/Login")]
    public async Task Login_is_accessible_without_a_redirect_loop(string path)
    {
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Tiếp tục khám phá", html);
        Assert.DoesNotContain("guest-access.js", html);
    }

    [Fact]
    public async Task Only_login_actions_allow_anonymous_MVC_access()
    {
        var actions = app.Services.GetRequiredService<Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorCollectionProvider>();
        var anonymous = actions.ActionDescriptors.Items
            .OfType<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()
            .Where(action => action.EndpointMetadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
            .ToList();
        Assert.Equal(3, anonymous.DistinctBy(action => action.MethodInfo).Count());
        Assert.All(anonymous, action =>
        {
            Assert.Equal("Account", action.ControllerName);
            Assert.Contains(action.ActionName, new[] { "Login", "Google" });
        });
    }

    [Fact]
    public async Task Google_sign_in_can_start_before_authentication()
    {
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/Account/Google?returnUrl=%2FGroups");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("accounts.google.com", response.Headers.Location!.Host);
    }

    [Fact]
    public async Task Authenticated_user_can_open_pages_and_logout_returns_to_login()
    {
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new System.Security.Claims.ClaimsIdentity(new[] {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Test Student")
        }, "Cookies");
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(
            new System.Security.Claims.ClaimsPrincipal(identity), new Microsoft.AspNetCore.Authentication.AuthenticationProperties(), "Cookies");
        client.DefaultRequestHeaders.Add("Cookie", options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket));
        using var page = await client.GetAsync("/Privacy");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.DoesNotContain("guest-access.js", html);
        var token = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Contains(logout.Headers.Location!.ToString(), new[] { "/Login", "/Account/Login" });
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("StudyHub.Auth=;"));
    }
}
