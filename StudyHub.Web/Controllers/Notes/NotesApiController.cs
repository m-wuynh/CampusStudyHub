using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;

namespace StudyHub.Web.Controllers.Notes;

[ApiController]
[Authorize]
[Route("api/notes/notes")]
public sealed class NotesApiController(StudyHubDbContext db, ICurrentUser currentUser) : ControllerBase
{
    private const int MaxDeltaBytes = 2 * 1024 * 1024;
    private const string EmptyDelta = "{\"ops\":[{\"insert\":\"\\n\"}]}";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteListItem>>> List(CancellationToken cancellationToken)
    {
        // The PDF page migration is staged separately. Keep the library and PDF sharing usable
        // against an older database while it is awaiting the schema safety review.
        var query = db.Notes.AsNoTracking().Where(note => note.OwnerUserId == currentUser.UserId &&
            !note.IsDeleted && note.StudyGroupId == null && note.Visibility == "Private");
        if (await HasPdfPageSchemaAsync(cancellationToken))
            query = query.Where(note => !db.PdfPageNotes.Any(page => page.NoteId == note.NoteId));
        var notes = await query
            .OrderByDescending(note => note.UpdatedAtUtc)
            .Select(note => new { note.NoteId, note.Title, note.UpdatedAtUtc,
                note.IsArchived, note.IsPinned, note.RowVersion })
            .ToListAsync(cancellationToken);
        return Ok(notes.Select(note => new NoteListItem(note.NoteId, note.Title, note.UpdatedAtUtc,
            note.IsArchived, note.IsPinned, Convert.ToBase64String(note.RowVersion))));
    }

    private async Task<bool> HasPdfPageSchemaAsync(CancellationToken cancellationToken) =>
        await db.Database.SqlQueryRaw<int>(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.PdfPageNotes', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync(cancellationToken) == 1;

    [HttpGet("pdf/{documentId:long}")]
    public async Task<IActionResult> PdfNotebook(long documentId, CancellationToken cancellationToken)
    {
        var document = await OwnedPdf(documentId).FirstOrDefaultAsync(cancellationToken);
        if (document is null) return NotFound();
        if (!await HasPdfPageSchemaAsync(cancellationToken))
            return StatusCode(503, new { message = "Sổ ghi chú theo trang PDF đang chờ migration database." });
        var pages = await db.PdfPageNotes.AsNoTracking()
            .Where(page => page.DocumentId == documentId && page.OwnerUserId == currentUser.UserId)
            .Join(OwnedNotes().AsNoTracking(), page => page.NoteId, note => note.NoteId,
                (page, note) => new { note.NoteId, page.PageIndex, note.Title, note.IsArchived, note.UpdatedAtUtc })
            .OrderBy(page => page.PageIndex)
            .ToListAsync(cancellationToken);
        return Ok(new { documentId, title = document.Title, pages });
    }

    [HttpPost("pdf/{documentId:long}/pages/{pageIndex:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePdfPage(long documentId, int pageIndex, CancellationToken cancellationToken)
    {
        if (pageIndex is < 0 or > 100_000) return BadRequest(new { message = "Số trang không hợp lệ." });
        var document = await OwnedPdf(documentId).FirstOrDefaultAsync(cancellationToken);
        if (document is null) return NotFound();
        if (!await HasPdfPageSchemaAsync(cancellationToken))
            return StatusCode(503, new { message = "Sổ ghi chú theo trang PDF đang chờ migration database." });
        if (document.IsArchived) return Conflict(new { message = "PDF đã lưu trữ, không thể thêm ghi chú." });
        var version = await db.DocumentVersions.AsNoTracking()
            .Where(item => item.DocumentId == documentId)
            .OrderByDescending(item => item.DocumentVersionId)
            .Select(item => new { item.PageCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (version is null) return NotFound(new { message = "PDF chưa có phiên bản hợp lệ." });
        if (version.PageCount is int count && pageIndex >= count)
            return BadRequest(new { message = "Trang nằm ngoài PDF." });
        var existingId = await db.PdfPageNotes.AsNoTracking()
            .Where(page => page.OwnerUserId == currentUser.UserId && page.DocumentId == documentId && page.PageIndex == pageIndex)
            .Select(page => (long?)page.NoteId).FirstOrDefaultAsync(cancellationToken);
        if (existingId is long noteId) return Ok(new { id = noteId, pageIndex, created = false });
        var now = DateTime.UtcNow;
        var note = new Note
        {
            OwnerUserId = currentUser.UserId,
            Title = $"Trang {pageIndex + 1}", Body = string.Empty, BodyFormat = "PlainText",
            Visibility = "Private", ContentDeltaJson = EmptyDelta, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Notes.Add(note);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            db.PdfPageNotes.Add(new PdfPageNote
            {
                NoteId = note.NoteId, OwnerUserId = currentUser.UserId,
                DocumentId = documentId, PageIndex = pageIndex
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            // The unique index also covers simultaneous creation in another tab.
            var concurrentId = await db.PdfPageNotes.AsNoTracking().Where(page =>
                page.OwnerUserId == currentUser.UserId && page.DocumentId == documentId && page.PageIndex == pageIndex)
                .Select(page => (long?)page.NoteId).FirstOrDefaultAsync(cancellationToken);
            if (concurrentId is null) throw;
            return Ok(new { id = concurrentId.Value, pageIndex, created = false });
        }
        return Ok(new { id = note.NoteId, pageIndex, created = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<NoteDetails>> Create([FromBody] CreateNoteRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return BadRequest(new { message = "Tên ghi chú phải có từ 1 đến 200 ký tự." });

        var now = DateTime.UtcNow;
        var note = new Note
        {
            OwnerUserId = currentUser.UserId,
            Title = title,
            Body = string.Empty,
            BodyFormat = "PlainText",
            Visibility = "Private",
            IsDeleted = false,
            IsArchived = false,
            ContentDeltaJson = EmptyDelta,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = note.NoteId }, ToDetails(note, []));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<NoteDetails>> Get(long id, CancellationToken cancellationToken)
    {
        var note = await OwnedNotes().FirstOrDefaultAsync(item => item.NoteId == id, cancellationToken);
        if (note is null) return NotFound();
        if (!await CanAccessNoteDocumentAsync(id, cancellationToken)) return NotFound();
        var references = await ReferenceQuery(id).ToListAsync(cancellationToken);
        return Ok(ToDetails(note, references));
    }

    [HttpPut("{id:long}")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxDeltaBytes + 64 * 1024)]
    public async Task<IActionResult> Save(long id, [FromBody] SaveNoteRequest request, CancellationToken cancellationToken)
    {
        var note = await OwnedNotes().FirstOrDefaultAsync(item => item.NoteId == id, cancellationToken);
        if (note is null) return NotFound();
        if (!await CanAccessNoteDocumentAsync(id, cancellationToken)) return NotFound();
        if (note.IsArchived) return Conflict(new { message = "Ghi chú đã lưu trữ, chỉ có thể xem." });
        if (!TokenMatches(note, request.VersionToken)) return Conflict(new { message = "Ghi chú đã được cập nhật ở nơi khác. Tải lại bản mới trước khi lưu." });
        if (!TryValidateDelta(request.ContentDelta, out var normalizedDelta, out var plainText, out var error))
            return BadRequest(new { message = error });

        note.Title = NormalizeTitle(request.Title);
        if (string.IsNullOrWhiteSpace(note.Title)) return BadRequest(new { message = "Tên ghi chú không được để trống." });
        note.ContentDeltaJson = normalizedDelta;
        note.Body = plainText;
        note.BodyFormat = "PlainText";
        note.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Ghi chú đã được cập nhật ở nơi khác. Tải lại bản mới trước khi lưu." });
        }
        return Ok(new { versionToken = Convert.ToBase64String(note.RowVersion), updatedAtUtc = note.UpdatedAtUtc });
    }

    [HttpPost("{id:long}/captures")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxDeltaBytes + 64 * 1024)]
    public async Task<IActionResult> SaveCapture(long id, [FromBody] SaveCaptureRequest request, CancellationToken cancellationToken)
    {
        var note = await OwnedNotes().FirstOrDefaultAsync(item => item.NoteId == id, cancellationToken);
        if (note is null) return NotFound(new { message = "Không tìm thấy ghi chú trong tài khoản hiện tại." });
        PdfPageNote? pageNote = null;
        if (await HasPdfPageSchemaAsync(cancellationToken))
        {
            pageNote = await db.PdfPageNotes.AsNoTracking().FirstOrDefaultAsync(item =>
                item.NoteId == id && item.OwnerUserId == currentUser.UserId, cancellationToken);
            if (pageNote is null)
                return BadRequest(new { message = "Hãy tạo ghi chú cho một trang trong sổ PDF trước khi chụp nội dung." });
        }
        if (note.IsArchived) return Conflict(new { message = "Ghi chú đã lưu trữ, không thể chụp thêm nội dung." });
        if (!TokenMatches(note, request.VersionToken)) return Conflict(new { message = "Ghi chú đã thay đổi. Tải lại bản mới trước khi chụp nội dung." });
        if (!TryValidateDelta(request.ContentDelta, out var normalizedDelta, out var plainText, out var error))
            return BadRequest(new { message = error });
        if (request.PageIndex is < 0 or > 100_000 || request.CaptureType is not ("Text" or "Area"))
            return BadRequest(new { message = "Thông tin tham chiếu PDF không hợp lệ." });
        if (!TryValidateAnchor(request.Anchor, out var anchorJson))
            return BadRequest(new { message = "Vùng tham chiếu phải có tọa độ dương, hữu hạn." });

        var version = await db.DocumentVersions.AsNoTracking()
            .Where(item => item.DocumentVersionId == request.DocumentVersionId)
            .Select(item => new { item.DocumentVersionId, item.DocumentId, item.PageCount, item.OriginalFileName })
            .FirstOrDefaultAsync(cancellationToken);
        if (version is null) return NotFound(new { message = "Không tìm thấy phiên bản PDF." });
        var document = await db.Documents.FirstOrDefaultAsync(item => item.DocumentId == version.DocumentId &&
            item.OwnerUserId == currentUser.UserId && !item.IsDeleted, cancellationToken);
        if (document is null) return NotFound(new { message = "PDF không còn trong thư viện của tài khoản hiện tại." });
        if (document.IsArchived) return Conflict(new { message = "PDF đã lưu trữ, không thể tạo tham chiếu mới." });
        if (version.PageCount is int pageCount && request.PageIndex >= pageCount)
            return BadRequest(new { message = "Trang tham chiếu nằm ngoài tài liệu." });
        if (pageNote is not null &&
            (pageNote.DocumentId != version.DocumentId || pageNote.PageIndex != request.PageIndex))
            return BadRequest(new { message = "Chỉ có thể chụp nội dung từ đúng trang của ghi chú PDF." });

        note.ContentDeltaJson = normalizedDelta;
        note.Body = plainText;
        note.BodyFormat = "PlainText";
        note.UpdatedAtUtc = DateTime.UtcNow;
        db.NoteReferenceLinks.Add(new NoteReferenceLink
        {
            NoteId = note.NoteId,
            DocumentVersionId = version.DocumentVersionId,
            PageIndex = request.PageIndex,
            CaptureType = request.CaptureType,
            AnchorJson = anchorJson,
            CapturedText = request.CaptureType == "Text" ? Truncate(request.CapturedText, 12_000) : null,
            CreatedAtUtc = note.UpdatedAtUtc
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Ghi chú vừa được cập nhật ở nơi khác. Không lưu được nội dung chụp." });
        }
        return Ok(new { versionToken = Convert.ToBase64String(note.RowVersion), updatedAtUtc = note.UpdatedAtUtc });
    }

    [HttpPost("{id:long}/archive")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Archive(long id, CancellationToken cancellationToken) => SetArchive(id, true, cancellationToken);

    [HttpPost("{id:long}/restore")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Restore(long id, CancellationToken cancellationToken) => SetArchive(id, false, cancellationToken);

    [HttpDelete("{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var note = await OwnedNotes().FirstOrDefaultAsync(item => item.NoteId == id, cancellationToken);
        if (note is null) return NotFound();
        if (!await CanAccessNoteDocumentAsync(id, cancellationToken)) return NotFound();
        if (await HasPdfPageSchemaAsync(cancellationToken))
        {
            var page = await db.PdfPageNotes.FirstOrDefaultAsync(item => item.NoteId == id, cancellationToken);
            if (page is not null) db.PdfPageNotes.Remove(page);
        }
        note.IsDeleted = true;
        note.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> SetArchive(long id, bool archived, CancellationToken cancellationToken)
    {
        var note = await OwnedNotes().FirstOrDefaultAsync(item => item.NoteId == id, cancellationToken);
        if (note is null) return NotFound();
        if (!await CanAccessNoteDocumentAsync(id, cancellationToken)) return NotFound();
        note.IsArchived = archived;
        note.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<Document> OwnedPdf(long documentId) => db.Documents.Where(document =>
        document.DocumentId == documentId && document.OwnerUserId == currentUser.UserId &&
        !document.IsDeleted && document.ResourceType == "File");

    private IQueryable<Note> OwnedNotes() => db.Notes.Where(note => note.OwnerUserId == currentUser.UserId &&
        !note.IsDeleted && note.StudyGroupId == null && note.Visibility == "Private");

    private async Task<bool> CanAccessNoteDocumentAsync(long noteId, CancellationToken cancellationToken)
    {
        if (!await HasPdfPageSchemaAsync(cancellationToken)) return true;
        var documentId = await db.PdfPageNotes.AsNoTracking()
            .Where(page => page.NoteId == noteId && page.OwnerUserId == currentUser.UserId)
            .Select(page => (long?)page.DocumentId).FirstOrDefaultAsync(cancellationToken);
        return documentId is null || await OwnedPdf(documentId.Value).AnyAsync(cancellationToken);
    }

    private IQueryable<NoteReferenceItem> ReferenceQuery(long noteId) =>
        from reference in db.NoteReferenceLinks.AsNoTracking()
        join version in db.DocumentVersions.AsNoTracking() on reference.DocumentVersionId equals version.DocumentVersionId
        join document in db.Documents.AsNoTracking() on version.DocumentId equals document.DocumentId
        where reference.NoteId == noteId && document.OwnerUserId == currentUser.UserId && !document.IsDeleted
        orderby reference.CreatedAtUtc descending
        select new NoteReferenceItem(reference.NoteReferenceLinkId, document.DocumentId, version.DocumentVersionId,
            document.Title, version.OriginalFileName, reference.PageIndex, reference.CaptureType,
            reference.AnchorJson, reference.CapturedText, reference.CreatedAtUtc);

    private static NoteDetails ToDetails(Note note, IReadOnlyList<NoteReferenceItem> references) =>
        new(note.NoteId, note.Title, note.ContentDeltaJson ?? DeltaFromPlainText(note.Body), note.UpdatedAtUtc,
            note.IsArchived, note.IsPinned, Convert.ToBase64String(note.RowVersion), references);

    private static bool TokenMatches(Note note, string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        try { return Convert.FromBase64String(token).AsSpan().SequenceEqual(note.RowVersion); }
        catch (FormatException) { return false; }
    }

    private static string NormalizeTitle(string? title) => title?.Trim() is { Length: <= 200 } clean ? clean : string.Empty;

    private static bool TryValidateDelta(JsonElement delta, out string normalized, out string plainText, out string message)
    {
        normalized = string.Empty;
        plainText = string.Empty;
        message = "Delta ghi chú không hợp lệ.";
        if (delta.ValueKind != JsonValueKind.Object || !delta.TryGetProperty("ops", out var operations) ||
            operations.ValueKind != JsonValueKind.Array || operations.GetArrayLength() > 50_000)
            return false;

        var text = new StringBuilder();
        foreach (var operation in operations.EnumerateArray())
        {
            if (operation.ValueKind != JsonValueKind.Object || !operation.TryGetProperty("insert", out var insert)) return false;
            if (operation.EnumerateObject().Any(property => property.Name is not ("insert" or "attributes"))) return false;
            if (operation.TryGetProperty("attributes", out var attributes) && !AreAllowedAttributes(attributes)) return false;
            if (insert.ValueKind == JsonValueKind.String)
            {
                text.Append(insert.GetString());
                continue;
            }
            if (insert.ValueKind != JsonValueKind.Object || insert.EnumerateObject().Count() != 1 ||
                !insert.TryGetProperty("image", out var image) || image.ValueKind != JsonValueKind.String ||
                !IsAllowedImage(image.GetString()))
                return false;
        }
        normalized = delta.GetRawText();
        if (Encoding.UTF8.GetByteCount(normalized) > MaxDeltaBytes)
        {
            message = "Ghi chú vượt quá giới hạn 2 MB.";
            return false;
        }
        plainText = text.ToString();
        message = string.Empty;
        return true;
    }

    private static bool IsAllowedImage(string? value)
    {
        if (value is null || value.Length > 1_400_000) return false;
        var comma = value.IndexOf(',');
        if (comma < 0 || !(value.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase))) return false;
        try { return Convert.FromBase64String(value[(comma + 1)..]).Length <= 1_000_000; }
        catch (FormatException) { return false; }
    }

    private static bool AreAllowedAttributes(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Object) return false;
        foreach (var attribute in attributes.EnumerateObject())
        {
            var valid = attribute.Name switch
            {
                "bold" or "italic" or "underline" or "strike" or "blockquote" => attribute.Value.ValueKind == JsonValueKind.True,
                "header" => attribute.Value.TryGetInt32(out var heading) && heading is >= 1 and <= 3,
                "list" => attribute.Value.ValueKind == JsonValueKind.String && attribute.Value.GetString() is "ordered" or "bullet",
                "link" => IsSafeLink(attribute.Value),
                _ => false
            };
            if (!valid) return false;
        }
        return true;
    }

    private static bool IsSafeLink(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || !Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri)) return false;
        return uri.Scheme is "https" or "http" or "mailto";
    }

    private static bool TryValidateAnchor(AnchorRequest? anchor, out string json)
    {
        json = string.Empty;
        if (anchor is null || !double.IsFinite(anchor.X) || !double.IsFinite(anchor.Y) ||
            !double.IsFinite(anchor.Width) || !double.IsFinite(anchor.Height) || anchor.X < 0 || anchor.Y < 0 ||
            anchor.Width <= 0 || anchor.Height <= 0 || anchor.X > 100_000 || anchor.Y > 100_000 ||
            anchor.Width > 100_000 || anchor.Height > 100_000) return false;
        json = JsonSerializer.Serialize(anchor, JsonOptions);
        return true;
    }

    private static string DeltaFromPlainText(string? body) => JsonSerializer.Serialize(new
    {
        ops = new[] { new { insert = string.IsNullOrEmpty(body) ? "\n" : body + "\n" } }
    }, JsonOptions);

    private static string? Truncate(string? value, int max) => value is { Length: > 0 } ? value[..Math.Min(value.Length, max)] : null;
}

public sealed record NoteListItem(long Id, string Title, DateTime UpdatedAtUtc, bool IsArchived, bool IsPinned, string VersionToken);
public sealed record NoteDetails(long Id, string Title, string ContentDeltaJson, DateTime UpdatedAtUtc,
    bool IsArchived, bool IsPinned, string VersionToken, IReadOnlyList<NoteReferenceItem> References);
public sealed record NoteReferenceItem(long Id, long DocumentId, long DocumentVersionId, string DocumentTitle,
    string FileName, int PageIndex, string CaptureType, string AnchorJson, string? CapturedText, DateTime CreatedAtUtc);
public sealed record CreateNoteRequest(string? Title);
public sealed record SaveNoteRequest(string? Title, JsonElement ContentDelta, string? VersionToken);
public sealed record AnchorRequest(double X, double Y, double Width, double Height);
public sealed record SaveCaptureRequest(string? VersionToken, long DocumentVersionId, int PageIndex,
    string? CaptureType, AnchorRequest? Anchor, string? CapturedText, JsonElement ContentDelta);
