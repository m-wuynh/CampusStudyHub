using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.DAL.Repositories.Notes;
using StudyHub.Web.ViewModels.StudyGroups;

namespace StudyHub.Web.Controllers.StudyGroups;

[Authorize, Route("Groups/{groupId:long}/Resources")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GroupResourcesController(GroupLearningService service, IWebHostEnvironment environment) : Controller
{
    [HttpGet("{kind}/{id:long}")]
    public async Task<IActionResult> Read(long groupId, string kind, long id, CancellationToken ct)
    {
        try
        {
            var content = await service.ReadResourceAsync(groupId, kind, id, ct);
            var pages = new List<SharedNotePage>();
            if (kind == "Note")
            {
                const string marker = "STUDYHUB_NOTEBOOK_V1\n";
                if (content.Body?.StartsWith(marker, StringComparison.Ordinal) == true)
                {
                    try { pages = JsonSerializer.Deserialize<List<SharedNotePage>>(content.Body[marker.Length..]) ?? []; }
                    catch (JsonException) { return BadRequest("Không thể đọc nội dung notebook."); }
                }
                else pages.Add(new SharedNotePage(content.Title, content.Body ?? ""));
            }
            return View("~/Views/StudyGroups/Resource.cshtml", new GroupResourceViewModel(groupId, id, content, pages));
        }
        catch (StudyGroupException e) { return StatusCode(e.StatusCode, e.Message); }
    }

    [HttpGet("Document/{id:long}/pdf")]
    public async Task<IActionResult> Pdf(long groupId, long id, CancellationToken ct)
    {
        try
        {
            var resource = await service.ReadResourceAsync(groupId, "Document", id, ct);
            const string prefix = "notes-pdf/";
            if (resource.StorageKey?.StartsWith(prefix, StringComparison.Ordinal) != true) return NotFound();
            var key = resource.StorageKey[prefix.Length..];
            if (!Guid.TryParseExact(key, "N", out _)) return NotFound();
            var files = new NotesFileRepository(Path.Combine(environment.ContentRootPath, "App_Data", "Notes"));
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(files.OpenPdf(key), "application/pdf", enableRangeProcessing: true);
        }
        catch (StudyGroupException e) { return StatusCode(e.StatusCode, e.Message); }
        catch (FileNotFoundException) { return NotFound("Tệp PDF không còn tồn tại."); }
        catch (DirectoryNotFoundException) { return NotFound("Tệp PDF không còn tồn tại."); }
    }
}
