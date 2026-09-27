using System.Text;
using Microsoft.AspNetCore.Mvc;
using StudyHub.BLL.DTOs.Notes;
using StudyHub.BLL.Services.Notes;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.Notes;

namespace StudyHub.Web.Controllers.Notes;

[Route("Notes/Export")]
public sealed class NotesExportController : Controller
{
    private readonly INotebookService notebooks;
    private readonly INotesWorkspaceService service;
    private readonly NotesUserResolver users;
    private readonly IWebHostEnvironment environment;

    public NotesExportController(
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

    private Task<NotesUserDto> CurrentAsync(CancellationToken ct)
    {
        return users.ResolveAsync(
            User.Identity?.IsAuthenticated == true,
            User.FindFirst(NotesUserResolver.UserIdClaim)?.Value,
            environment.IsDevelopment(),
            ct);
    }

    private static bool IsMarkdown(string format)
    {
        if (format is not ("txt" or "md"))
            throw new NotesException("Chọn định dạng txt hoặc md.");

        return format == "md";
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Download(
        long id,
        string format = "txt",
        Guid? pageId = null,
        CancellationToken ct = default)
    {
        try
        {
            var markdown = IsMarkdown(format);
            var current = await CurrentAsync(ct);
            var book = await notebooks.GetAsync(
                current.Id, id, pageId, ct);

            IEnumerable<NotebookPageDto> selected = book.Pages;

            if (pageId.HasValue)
                selected = selected.Where(p => p.Id == pageId.Value);

            var text = new StringBuilder();
            text.AppendLine(markdown ? $"# {book.Title}" : book.Title);
            text.AppendLine();

            foreach (var page in selected)
            {
                var number = book.Pages.FindIndex(p => p.Id == page.Id) + 1;
                var heading = $"Trang {number}: {page.Title}";

                text.AppendLine(markdown ? $"## {heading}" : heading);
                text.AppendLine();
                text.AppendLine(page.Body);
                text.AppendLine();
            }

            var suffix = pageId.HasValue ? "-trang" : "-notebook";

            return TextFile(
                text.ToString(),
                book.Title + suffix,
                format);
        }
        catch (NotesException ex)
        {
            return Problem(title: ex.Message, statusCode: ex.Status);
        }
    }

    [HttpGet("PdfNotes/{id:long}")]
    public async Task<IActionResult> PdfNotes(
        long id,
        int pageCount,
        string format = "txt",
        CancellationToken ct = default)
    {
        try
        {
            var markdown = IsMarkdown(format);

            if (pageCount is < 1 or > 10000)
                throw new NotesException("Số trang không hợp lệ.");

            var current = await CurrentAsync(ct);
            var document = await service.GetPdfAsync(current.Id, id, ct);

            var text = new StringBuilder();
            text.AppendLine(markdown
                ? $"# Ghi chú: {document.Title}"
                : $"Ghi chú: {document.Title}");
            text.AppendLine();

            var hasNotes = false;

            for (var page = 1; page <= pageCount; page++)
            {
                ct.ThrowIfCancellationRequested();

                var note = await service.GetPageAsync(
                    current.Id, id, page, ct);

                if (string.IsNullOrWhiteSpace(note.Text))
                    continue;

                hasNotes = true;
                text.AppendLine(markdown
                    ? $"## Trang {page}"
                    : $"Trang {page}");

                text.AppendLine();
                text.AppendLine(note.Text);
                text.AppendLine();
            }

            if (!hasNotes)
                text.AppendLine("Chưa có ghi chú chữ.");

            return TextFile(
                text.ToString(),
                document.Title + "-ghi-chu",
                format);
        }
        catch (NotesException ex)
        {
            return Problem(title: ex.Message, statusCode: ex.Status);
        }
    }

    private FileContentResult TextFile(
        string text,
        string title,
        string format)
    {
        const string invalid = "<>:\"/\\|?*";

        var safe = new string(title
            .Select(c => char.IsControl(c) || invalid.Contains(c) ? '_' : c)
            .ToArray())
            .Trim()
            .TrimEnd('.');

        if (safe.Length > 100)
            safe = safe[..100];

        if (safe.Length > 0 && char.IsHighSurrogate(safe[^1]))
            safe = safe[..^1];

        if (string.IsNullOrWhiteSpace(safe))
            safe = "ghi-chu";

        // Tiền tố tránh các tên file dành riêng của Windows.
        var fileName = $"StudyHub-{safe}.{format}";
        var encoding = new UTF8Encoding(true);
        var bytes = encoding.GetPreamble()
            .Concat(encoding.GetBytes(text))
            .ToArray();

        Response.Headers["Cache-Control"] = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(
            bytes,
            format == "md"
                ? "text/markdown; charset=utf-8"
                : "text/plain; charset=utf-8",
            fileName);
    }
}
