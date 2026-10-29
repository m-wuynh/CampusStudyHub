using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Flashcards;
using StudyHub.BLL.Services.Flashcards.Implementations;
using StudyHub.BLL.Services.Flashcards.Interfaces;
using StudyHub.BLL.Services.NotesV3.Pdfs;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.Auth;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.NotesV3;
using StudyHub.DAL.Repositories.StudyGroups;
using StudyHub.Web.Authentication;
using StudyHub.Web.Hubs;
using StudyHub.Web.Services.NotesV3;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
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
    INotesV3Repository,
    EfNotesV3Repository>();

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

// NotesV3 access
builder.Services.AddScoped<
    StudyHub.BLL.Services.NotesV3.INotesV3AccessService,
    StudyHub.BLL.Services.NotesV3.NotesV3AccessService>();

// NotesV3 notebooks
builder.Services.AddScoped<
    StudyHub.BLL.Services.NotesV3.Notebooks.INotebookService,
    StudyHub.BLL.Services.NotesV3.Notebooks.NotebookService>();

// NotesV3 PDF
builder.Services.AddScoped<
    IPdfFileStorage,
    LocalPdfFileStorage>();

builder.Services.AddScoped<
    IPdfService,
    PdfService>();

// NotesV3 PDF page notes
builder.Services.AddScoped<
    StudyHub.BLL.Services.NotesV3.PageNotes.IPageNotesService,
    StudyHub.BLL.Services.NotesV3.PageNotes.PageNotesService>();

// NotesV3 Group sharing
builder.Services.AddScoped<
    StudyHub.BLL.Services.NotesV3.Sharing.IGroupShareService,
    StudyHub.BLL.Services.NotesV3.Sharing.GroupShareService>();

// NotesV3 collaboration
builder.Services.AddScoped<
    StudyHub.BLL.Services.NotesV3.Collaboration.ICollaborationService,
    StudyHub.BLL.Services.NotesV3.Collaboration.CollaborationService>();

// NotesV3 annotations
builder.Services.AddScoped<
    StudyHub.BLL.Services.NotesV3.Annotations.IAnnotationService,
    StudyHub.BLL.Services.NotesV3.Annotations.AnnotationService>();

// NotesV3 references
builder.Services.AddScoped<
    StudyHub.BLL.Services.NotesV3.References.IReferenceService,
    StudyHub.BLL.Services.NotesV3.References.ReferenceService>();

// NotesV3 dashboard
builder.Services.AddScoped<
    StudyHub.BLL.Services.NotesV3.Dashboard.INotesDashboardService,
    StudyHub.BLL.Services.NotesV3.Dashboard.NotesDashboardService>();

// NotesV3 MVC exception filter
builder.Services.AddScoped<
    StudyHub.Web.Filters.NotesV3ExceptionFilter>();

// NotesV3 SignalR filter
builder.Services.AddScoped<NotesV3HubFilter>();

builder.Services.Configure<HubOptions<NotesV3Hub>>(options =>
{
    options.MaximumReceiveMessageSize = 4 * 1024 * 1024;
    options.MaximumParallelInvocationsPerClient = 1;
    options.EnableDetailedErrors = false;
    options.AddFilter<NotesV3HubFilter>();
});

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
app.MapHub<NotesV3Hub>("/hubs/notesv3");

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Expose the entry point to integration tests.
public partial class Program
{
}