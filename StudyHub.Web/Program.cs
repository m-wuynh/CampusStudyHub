using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Flashcards;
using StudyHub.BLL.Services.Flashcards.Implementations;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.BLL.Services.NotesV3.Annotations;
using StudyHub.BLL.Services.NotesV3.Notebooks;
using StudyHub.BLL.Services.NotesV3.Pages;
using StudyHub.BLL.Services.NotesV3.PagesNotes;
using StudyHub.BLL.Services.NotesV3.Pdfs;
using StudyHub.BLL.Services.NotesV3.Sharing;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.Auth;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.StudyGroups;
using StudyHub.Web.Authentication;
using StudyHub.Web.Hubs;
using StudyHub.Web.Services.NotesV3;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<StudyHub.Web.Filters.NotesV3ExceptionFilter>();
    options.Conventions.Add(
        new StudyHub.Web.Configuration.DisableLegacyNotesControllers());
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddSignalR();

// Authentication
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;

    options.DefaultSignInScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;
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

var googleClientId =
    builder.Configuration["Authentication:Google:ClientId"];

var googleClientSecret =
    builder.Configuration["Authentication:Google:ClientSecret"];

if (!string.IsNullOrWhiteSpace(googleClientId) &&
    !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";

        options.ClaimActions.Add(
            new JsonKeyClaimAction(
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

// Database
var studyHubConnection =
    builder.Configuration.GetConnectionString("StudyHub")
    ?? throw new InvalidOperationException(
        "Thiếu connection string 'StudyHub'.");

builder.Services.AddDbContext<StudyHubDbContext>(options =>
{
    options.UseSqlServer(studyHubConnection);
});

// DAL repositories
builder.Services.AddScoped<
    IStudyGroupRepository,
    SqlStudyGroupRepository>();

builder.Services.AddScoped<
    IGoogleAuthRepository,
    GoogleAuthRepository>();

builder.Services.AddScoped<
    IRepository,
    EfRepository>();

builder.Services.AddScoped<
    IGroupLearningRepository,
    SqlGroupLearningRepository>();

// Authentication services
builder.Services.AddScoped<
    IAuthService,
    AuthService>();

builder.Services.AddScoped<
    ICurrentUser,
    HttpCurrentUser>();

// Study Group services
builder.Services.AddScoped<
    IStudyGroupService,
    StudyGroupService>();

builder.Services.AddScoped<GroupLearningService>();

// Flashcard services
builder.Services.AddScoped<
    IFlashcardDeckService,
    FlashcardDeckService>();

builder.Services.AddScoped<
    IFlashcardService,
    FlashcardService>();

// NotesV3: các màn hình MVC và nghiệp vụ tương ứng.
builder.Services.AddScoped<INotesV3AccessService, NotesV3AccessService>();
builder.Services.AddScoped<INotesIndexService, NotesIndexService>();
builder.Services.AddScoped<INotebookService, NotebookService>();
builder.Services.AddScoped<IPageService, PageService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IPageNotesService, PageNotesService>();
builder.Services.AddScoped<IPdfMarksService, PdfMarksService>();
builder.Services.AddScoped<IGroupShareService, GroupShareService>();
builder.Services.AddScoped<IPdfFileStorage, LocalPdfFileStorage>();

// NotesV3 MVC exception filter
builder.Services.AddScoped<
    StudyHub.Web.Filters.NotesV3ExceptionFilter>();

// Other application services
builder.Services.AddScoped<
    StudyHub.BLL.Services.Grades.IGradeService,
    StudyHub.BLL.Services.Grades.GradeService>();

builder.Services.AddScoped<
    StudyHub.BLL.Services.Grades.IGoalService,
    StudyHub.BLL.Services.Grades.GoalService>();

builder.Services.AddScoped<
    StudyHub.BLL.Services.Grades.IGradeBookService,
    StudyHub.BLL.Services.Grades.GradeBookService>();

builder.Services.AddScoped<
    StudyHub.BLL.Services.Calendar.ICalendarService,
    StudyHub.BLL.Services.Calendar.CalendarService>();

builder.Services.AddTransient<
    StudyHub.BLL.Services.Email.IEmailService,
    StudyHub.BLL.Services.Email.EmailService>();

builder.Services.AddHostedService<
    StudyHub.Web.BackgroundServices
        .EmailNotificationBackgroundService>();

var app = builder.Build();

// Ignore expected request cancellation when a browser leaves a page.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (OperationCanceledException)
        when (context.RequestAborted.IsCancellationRequested)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 499;
        }
    }
});

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
            context.Response.StatusCode =
                StatusCodes.Status404NotFound;

            return;
        }

        await next();
    });
}

var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".mjs"] = "text/javascript";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllers();

app.MapHub<StudyGroupHub>("/hubs/study-groups");

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Expose the entry point to integration tests.
public partial class Program
{
}