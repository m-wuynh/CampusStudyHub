using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;
using StudyHub.Web.Services.Notes;

namespace StudyHub.Web.Controllers.Notes;

[ApiController]
[Authorize]
[Route("api/notes/documents")]
public sealed class DocumentsApiController(
    StudyHubDbContext db,
    ICurrentUser currentUser,
    IPdfStorage storage) : ControllerBase
{
    private const long MaxPdfBytes = 25 * 1024 * 1024;
    private const int MaxAnnotations = 5000;
    private const int MaxAnnotationPayloadBytes = 8 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentListItem>>> List(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var documents = await db.Documents.AsNoTracking()
            .Where(d => d.OwnerUserId == userId && !d.IsDeleted && d.ResourceType == "File")
            .Select(d => new
            {
                d.DocumentId,
                d.Title,
                d.OriginalFileName,
                d.FileSizeBytes,
                d.CreatedAtUtc,
                d.UpdatedAtUtc,
                d.IsArchived,
                IsPinned = d.DocumentBookmarks.Any(b => b.UserId == userId),
                VersionId = db.DocumentVersions.Where(v => v.DocumentId == d.DocumentId)
                    .OrderByDescending(v => v.DocumentVersionId).Select(v => (long?)v.DocumentVersionId).FirstOrDefault()
            })
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(documents.Select(d => new DocumentListItem(
            d.DocumentId, d.Title, d.OriginalFileName ?? d.Title, d.FileSizeBytes ?? 0,
            d.CreatedAtUtc, d.UpdatedAtUtc, d.IsArchived, d.IsPinned, d.VersionId)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxPdfBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxPdfBytes + 1024 * 1024)]
    public async Task<ActionResult<DocumentListItem>> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? title,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Chọn một file PDF trước khi tải lên." });
        if (file.Length > MaxPdfBytes)
            return BadRequest(new { message = "PDF vượt quá giới hạn 25 MB." });
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Chỉ chấp nhận file có đuôi .pdf." });

        await using (var stream = file.OpenReadStream())
        {
            var header = new byte[Math.Min(1024, (int)file.Length)];
            var read = await stream.ReadAsync(header, cancellationToken);
            if (!System.Text.Encoding.ASCII.GetString(header, 0, read).Contains("%PDF-", StringComparison.Ordinal))
                return BadRequest(new { message = "Nội dung file không phải PDF hợp lệ." });
        }

        var originalName = Path.GetFileName(file.FileName);
        if (originalName.Length > 255) originalName = originalName[^255..];
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(originalName).Trim() : title.Trim();
        if (cleanTitle.Length is < 1 or > 200)
            return BadRequest(new { message = "Tên tài liệu phải có từ 1 đến 200 ký tự." });

        var stored = await storage.SaveAsync(file, cancellationToken);
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var document = new Document
            {
                OwnerUserId = currentUser.UserId,
                Title = cleanTitle,
                ResourceType = "File",
                StorageKey = stored.StorageKey,
                OriginalFileName = originalName,
                MimeType = "application/pdf",
                FileSizeBytes = file.Length,
                Visibility = "Private",
                IsDeleted = false,
                IsArchived = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync(cancellationToken);

            var version = new DocumentVersion
            {
                DocumentId = document.DocumentId,
                StorageKey = stored.StorageKey,
                Sha256 = stored.Sha256,
                OriginalFileName = originalName,
                MimeType = "application/pdf",
                FileSizeBytes = file.Length,
                CreatedAtUtc = now
            };
            db.DocumentVersions.Add(version);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return CreatedAtAction(nameof(Get), new { id = document.DocumentId },
                new DocumentListItem(document.DocumentId, document.Title, originalName, file.Length,
                    now, now, false, false, version.DocumentVersionId));
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            storage.Delete(stored.StorageKey);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DocumentDetails>> Get(long id, [FromQuery] long? versionId, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound();
        var version = await ResolveVersion(id, versionId, cancellationToken);
        if (version is null) return NotFound();
        return Ok(new DocumentDetails(document.DocumentId, document.Title, version.DocumentVersionId,
            version.OriginalFileName, version.FileSizeBytes, document.IsArchived));
    }

    [HttpGet("{id:long}/file")]
    public async Task<IActionResult> File(long id, [FromQuery] long? versionId, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound(new { message = "Không tìm thấy tài liệu trong thư viện của tài khoản hiện tại." });
        var version = await ResolveVersion(id, versionId, cancellationToken);
        if (version is null) return NotFound(new { message = "Tài liệu chưa có bản PDF được lưu trong cơ sở dữ liệu." });
        var path = storage.GetPhysicalPath(version.StorageKey);
        if (!System.IO.File.Exists(path)) return NotFound(new { message = "PDF không còn trong kho lưu trữ." });
        return PhysicalFile(path, "application/pdf", enableRangeProcessing: true);
    }

    [HttpHead("{id:long}/file")]
    public async Task<IActionResult> FileMetadata(long id, [FromQuery] long? versionId, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound(new { message = "Không tìm thấy tài liệu trong thư viện của tài khoản hiện tại." });
        var version = await ResolveVersion(id, versionId, cancellationToken);
        if (version is null) return NotFound(new { message = "Tài liệu chưa có bản PDF được lưu trong cơ sở dữ liệu." });
        var path = storage.GetPhysicalPath(version.StorageKey);
        if (!System.IO.File.Exists(path)) return NotFound();

        Response.ContentType = "application/pdf";
        Response.ContentLength = new FileInfo(path).Length;
        Response.Headers["Accept-Ranges"] = "bytes";
        return Ok();
    }

    [HttpGet("{id:long}/annotations")]
    public async Task<IActionResult> GetAnnotations(long id, [FromQuery] long? versionId, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound();
        var version = await ResolveVersion(id, versionId, cancellationToken);
        if (version is null) return NotFound();
        var rows = await db.DocumentAnnotations.AsNoTracking()
            .Where(a => a.DocumentVersionId == version.DocumentVersionId && a.OwnerUserId == currentUser.UserId)
            .OrderBy(a => a.PageIndex).ThenBy(a => a.AnnotationKey)
            .Select(a => a.PayloadJson).ToListAsync(cancellationToken);
        return Content($"[{string.Join(",", rows)}]", "application/json");
    }

    [HttpPut("{id:long}/annotations")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxAnnotationPayloadBytes)]
    public async Task<IActionResult> SaveAnnotations(
        long id,
        [FromQuery] long? versionId,
        [FromBody] JsonElement annotations,
        CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound();
        if (document.IsArchived) return Conflict(new { message = "Tài liệu đã lưu trữ, annotation chỉ đọc." });
        if (annotations.ValueKind != JsonValueKind.Array || annotations.GetArrayLength() > MaxAnnotations)
            return BadRequest(new { message = "Danh sách annotation không hợp lệ hoặc vượt quá giới hạn." });
        if (System.Text.Encoding.UTF8.GetByteCount(annotations.GetRawText()) > MaxAnnotationPayloadBytes)
            return BadRequest(new { message = "Annotation vượt quá giới hạn 8 MB." });

        var version = await ResolveVersion(id, versionId, cancellationToken);
        if (version is null) return NotFound();
        var replacements = new List<DocumentAnnotation>();
        foreach (var item in annotations.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("pageIndex", out var pageValue) || !pageValue.TryGetInt32(out var pageIndex) ||
                pageIndex < 0 || pageIndex > 100_000 || (version.PageCount is int pageCount && pageIndex >= pageCount))
                return BadRequest(new { message = "Annotation thiếu id hoặc pageIndex hợp lệ." });

            var key = idValue.GetString();
            if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
                return BadRequest(new { message = "Annotation id không hợp lệ." });
            replacements.Add(new DocumentAnnotation
            {
                DocumentVersionId = version.DocumentVersionId,
                OwnerUserId = currentUser.UserId,
                AnnotationKey = key,
                PageIndex = pageIndex,
                PayloadJson = item.GetRawText(),
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        if (replacements.Select(a => a.AnnotationKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != replacements.Count)
            return BadRequest(new { message = "Danh sách annotation có id trùng nhau." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var existing = db.DocumentAnnotations.Where(a => a.DocumentVersionId == version.DocumentVersionId &&
            a.OwnerUserId == currentUser.UserId);
        db.DocumentAnnotations.RemoveRange(existing);
        await db.SaveChangesAsync(cancellationToken);
        db.DocumentAnnotations.AddRange(replacements);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { count = replacements.Count, savedAtUtc = DateTime.UtcNow });
    }

    [HttpPut("{id:long}/page-count")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePageCount(long id, [FromQuery] long? versionId, [FromBody] UpdatePageCountRequest request,
        CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound();
        var version = await ResolveVersion(id, versionId, cancellationToken);
        if (version is null) return NotFound();
        if (request.PageCount is < 1 or > 100_000)
            return BadRequest(new { message = "Số trang PDF không hợp lệ." });
        version.PageCount = request.PageCount;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:long}/title")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(long id, [FromBody] RenameDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound();
        if (document.IsArchived) return Conflict(new { message = "Không thể đổi tên tài liệu đã lưu trữ." });
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return BadRequest(new { message = "Tên tài liệu phải có từ 1 đến 200 ký tự." });
        document.Title = title;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/archive")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Archive(long id, CancellationToken cancellationToken) => SetArchive(id, true, cancellationToken);

    [HttpPost("{id:long}/restore")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Restore(long id, CancellationToken cancellationToken) => SetArchive(id, false, cancellationToken);

    [HttpPost("{id:long}/pin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pin(long id, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound();
        var bookmark = await db.DocumentBookmarks.FindAsync(new object[] { currentUser.UserId, id }, cancellationToken);
        if (bookmark is null)
            db.DocumentBookmarks.Add(new DocumentBookmark { UserId = currentUser.UserId, DocumentId = id, CreatedAtUtc = DateTime.UtcNow });
        else
            db.DocumentBookmarks.Remove(bookmark);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDelete(long id, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound();
        document.IsDeleted = true;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await DeactivateGroupSharesAsync(id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> SetArchive(long id, bool archived, CancellationToken cancellationToken)
    {
        var document = await GetOwnedDocument(id, cancellationToken);
        if (document is null) return NotFound();
        document.IsArchived = archived;
        document.Visibility = "Private";
        document.StudyGroupId = null;
        document.UpdatedAtUtc = DateTime.UtcNow;
        if (!archived) await DeactivateGroupSharesAsync(id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task DeactivateGroupSharesAsync(long documentId, CancellationToken cancellationToken)
    {
        var shares = await db.GroupDocumentShares.Where(item => item.DocumentId == documentId && item.IsActive)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var share in shares)
        {
            share.IsActive = false;
            share.UnsharedAtUtc = now;
        }
    }

    private Task<Document?> GetOwnedDocument(long id, CancellationToken cancellationToken) => db.Documents
        .FirstOrDefaultAsync(d => d.DocumentId == id && d.OwnerUserId == currentUser.UserId && !d.IsDeleted, cancellationToken);

    private Task<DocumentVersion?> LatestVersion(long documentId, CancellationToken cancellationToken) => db.DocumentVersions
        .Where(v => v.DocumentId == documentId)
        .OrderByDescending(v => v.DocumentVersionId)
        .FirstOrDefaultAsync(cancellationToken);

    private Task<DocumentVersion?> ResolveVersion(long documentId, long? versionId, CancellationToken cancellationToken) =>
        versionId is long selectedVersionId
            ? db.DocumentVersions.FirstOrDefaultAsync(v => v.DocumentId == documentId && v.DocumentVersionId == selectedVersionId, cancellationToken)
            : LatestVersion(documentId, cancellationToken);
}

public sealed record DocumentListItem(long Id, string Title, string FileName, long SizeBytes,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, bool IsArchived, bool IsPinned, long? VersionId);
public sealed record DocumentDetails(long Id, string Title, long VersionId, string FileName, long SizeBytes, bool IsArchived);
public sealed record RenameDocumentRequest(string? Title);
public sealed record UpdatePageCountRequest(int PageCount);
