using Microsoft.AspNetCore.Mvc;
using StudyHub.Web.ViewModels.Notes;
using System.Security.Claims;

namespace StudyHub.Web.Controllers.Notes;

[Route("Notes")]
public sealed class NotesController(IWebHostEnvironment environment) : Controller
{
    private readonly INotesWorkspaceService service;
    private readonly INotebookService notebooks;
    private readonly NotesUserResolver users;
    private readonly IWebHostEnvironment environment;
    private NotesUserDto current = null!;

    public NotesController(
        IRepository repository,
        IWebHostEnvironment environment)
    {
        this.environment = environment;
        users = new NotesUserResolver(repository);
        notebooks = new NotebookService(repository);

        service = new NotesWorkspaceService(
            repository,
            new NotesFileRepository(
                Path.Combine(
                    environment.ContentRootPath,
                    "App_Data",
                    "Notes")));
    }

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        try
        {
            current = await users.ResolveAsync(
                User.Identity?.IsAuthenticated == true,
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                environment.IsDevelopment(),
                HttpContext.RequestAborted);

            ViewData["NotesUser"] = current;
        }
        catch (NotesException ex)
        {
            context.Result = Problem(
                title: ex.Message,
                statusCode: ex.Status);
            return;
        }

        var executed = await next();

        if (executed.Exception is NotesException error)
        {
            executed.ExceptionHandled = true;
            executed.Result = Problem(
                title: error.Message,
                statusCode: error.Status);
        }
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["ShowNotesDevelopmentRoles"] = environment.IsDevelopment();
        return View("~/Views/Notes/Index.cshtml");
    }

    [HttpGet("Reader/{id:long}")]
    public IActionResult Reader(long id) => View("~/Views/Notes/Reader.cshtml", id);

    [HttpGet("Note/{id:long}")]
    public IActionResult Note(long id) => View("~/Views/Notes/Note.cshtml", id);

    [HttpGet("Groups/{groupId:long}")]
    public IActionResult GroupWorkspace(long groupId) => View("~/Views/Notes/GroupWorkspace.cshtml", groupId);

    [HttpGet("Groups/{groupId:long}/Documents/{documentId:long}")]
    public IActionResult GroupReader(long groupId, long documentId) =>
        View("~/Views/Notes/GroupReader.cshtml", new GroupPdfReaderModel(groupId, documentId));
}
