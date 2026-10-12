using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Flashcards;
using StudyHub.BLL.Services.Flashcards.Implementations;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Notes;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.Auth;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.StudyGroups;
using StudyHub.Web.Authentication;
using StudyHub.Web.Hubs;
using StudyHub.Web.Services.Notes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddSignalR();

builder.Services.AddScoped<IStudyGroupGateway, StudyGroupGateway>();

var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});

authentication.AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Cookie.Name = "StudyHub.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
        options.ClaimActions.Add(new JsonKeyClaimAction(
            "urn:google:picture",
            System.Security.Claims.ClaimValueTypes.String,
            "picture"));
        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.Redirect("/Login?error=google_failed");
            return Task.CompletedTask;
        };
    });
}

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var studyHubConnection = builder.Configuration.GetConnectionString("StudyHub")
    ?? throw new InvalidOperationException("Thiếu connection string 'StudyHub'.");

builder.Services.AddDbContext<StudyHubDbContext>(options => options.UseSqlServer(studyHubConnection));
builder.Services.AddScoped<IStudyGroupRepository, SqlStudyGroupRepository>();
builder.Services.AddScoped<IGoogleAuthRepository, GoogleAuthRepository>();
builder.Services.AddScoped<IRepository, EfRepository>();
builder.Services.AddSingleton<IPdfStorage, LocalPdfStorage>();
builder.Services.AddScoped<GroupNotesService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IStudyGroupService, StudyGroupService>();
builder.Services.AddScoped<IGroupLearningRepository, SqlGroupLearningRepository>();
builder.Services.AddScoped<GroupLearningService>();
builder.Services.AddScoped<IFlashcardDeckService, FlashcardDeckService>();
builder.Services.AddScoped<IFlashcardService, FlashcardService>();
builder.Services.AddScoped<StudyHub.BLL.Services.Grades.IGradeService, StudyHub.BLL.Services.Grades.GradeService>();
builder.Services.AddScoped<StudyHub.BLL.Services.Grades.IGoalService, StudyHub.BLL.Services.Grades.GoalService>();
builder.Services.AddScoped<StudyHub.BLL.Services.Grades.IGradeBookService, StudyHub.BLL.Services.Grades.GradeBookService>();
builder.Services.AddScoped<StudyHub.BLL.Services.Calendar.ICalendarService, StudyHub.BLL.Services.Calendar.CalendarService>();
builder.Services.AddTransient<StudyHub.BLL.Services.Email.IEmailService, StudyHub.BLL.Services.Email.EmailService>();
builder.Services.AddHostedService<StudyHub.Web.BackgroundServices.EmailNotificationBackgroundService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/phase0"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
}

var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".mjs"] = "text/javascript";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapControllers();
app.MapHub<StudyGroupHub>("/hubs/study-groups");

if (app.Environment.IsDevelopment())
{
    app.MapGet("/phase0/group-gateway-checks", async (
        CancellationToken cancellationToken) =>
    {
        IStudyGroupGateway gateway = new FakeStudyGroupGateway();
        var cases = new[]
        {
            await Check("active member in active group is allowed", 1, 10, true, gateway, cancellationToken),
            await Check("non-member in active group is denied", 2, 20, false, gateway, cancellationToken),
            await Check("member of missing group is denied", 1, 99, false, gateway, cancellationToken)
        };

        return Results.Ok(new { environment = "Development", cases, allPassed = cases.All(test => test.Passed) });
    }).AllowAnonymous();
}

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

static async Task<GatewayCheck> Check(
    string name,
    long userId,
    long groupId,
    bool expected,
    IStudyGroupGateway gateway,
    CancellationToken cancellationToken)
{
    var exists = await gateway.GroupExistsAndIsActiveAsync(groupId, cancellationToken);
    var isMember = await gateway.IsActiveMemberAsync(userId, groupId, cancellationToken);
    var allowed = exists && isMember;
    return new(name, expected, allowed, expected == allowed);
}

public sealed record GatewayCheck(string Name, bool ExpectedAllowed, bool ActualAllowed, bool Passed);

public partial class Program { }
