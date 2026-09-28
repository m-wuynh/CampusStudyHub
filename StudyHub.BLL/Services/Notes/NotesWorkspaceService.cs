using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Notes;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.Notes;

namespace StudyHub.BLL.Services.Notes;

public sealed class NotesWorkspaceService(
    IRepository repository,
    INotesFileRepository files) : INotesWorkspaceService
{
    public const long MaxUpload = 20 * 1024 * 1024;

    private const string Prefix = "notes-pdf/";

    private static TextNoteDto Map(Note note)
    {
        return new TextNoteDto(
            note.NoteId,
            note.Title,
            note.Body,
            note.BodyFormat,
            Convert.ToBase64String(note.RowVersion),
            note.IsPinned);
    }

    private static string Title(string? value)
    {
        value = (value ?? "").Trim();

        if (value.Length is < 1 or > 200)
        {
            throw new NotesException(
                "Tiêu đề phải từ 1–200 ký tự.");
        }

        return value;
    }

    private Task<Note?> FindText(
        long userId,
        long id,
        CancellationToken ct)
    {
        return repository.FirstOrDefaultAsync<Note>(
            n => n.NoteId == id
                 && n.OwnerUserId == userId
                 && !n.IsDeleted,
            ct);
    }

    private async Task<Document> FindPdf(
        long userId,
        long id,
        CancellationToken ct)
    {
        return await repository.FirstOrDefaultAsync<Document>(
            d => d.DocumentId == id
                 && d.OwnerUserId == userId
                 && !d.IsDeleted
                 && d.ResourceType == "File"
                 && d.MimeType == "application/pdf"
                 && d.StorageKey != null
                 && d.StorageKey.StartsWith(Prefix),
            ct)
            ?? throw new NotesException(
                "Không tìm thấy PDF của bạn.",
                404);
    }

    private static string Key(Document document)
    {
        return document.StorageKey![Prefix.Length..];
    }

    private static void CheckVersion(
        Note note,
        string? version)
    {
        if (version != Convert.ToBase64String(note.RowVersion))
        {
            throw new NotesException(
                "Ghi chú đã thay đổi ở tab khác. "
                + "Sao chép nội dung cần giữ rồi mở lại.",
                409);
        }
    }

    private async Task Commit(CancellationToken ct)
    {
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotesException(
                "Dữ liệu vừa thay đổi ở nơi khác. Hãy tải lại.",
                409);
        }
    }

    public async Task<NotesWorkspaceDto> ListAsync(
        long userId,
        string? search,
        CancellationToken ct)
    {
        search = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();

        var notes = await repository.Query<Note>()
            .Where(n =>
                n.OwnerUserId == userId
                && !n.IsDeleted
                && (search == null || n.Title.Contains(search)))
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAtUtc)
            .ThenByDescending(n => n.NoteId)
            .ToListAsync(ct);

        // Kiểm tra bookmark của đúng người dùng hiện tại.
        // EF tạo truy vấn SQL từ navigation DocumentBookmarks.
        var documents = await repository.Query<Document>()
            .Where(d =>
                d.OwnerUserId == userId
                && !d.IsDeleted
                && d.ResourceType == "File"
                && d.MimeType == "application/pdf"
                && d.StorageKey != null
                && d.StorageKey.StartsWith(Prefix)
                && (search == null || d.Title.Contains(search)))
            .OrderByDescending(d =>
                d.DocumentBookmarks.Any(b => b.UserId == userId))
            .ThenByDescending(d => d.CreatedAtUtc)
            .ThenByDescending(d => d.DocumentId)
            .Select(d => new PdfNoteDto(
                d.DocumentId,
                d.Title,
                d.FileSizeBytes ?? 0,
                d.DocumentBookmarks.Any(b => b.UserId == userId)))
            .ToListAsync(ct);

        return new NotesWorkspaceDto(
            notes.Select(Map).ToList(),
            documents);
    }

    public async Task<TextNoteDto> GetTextAsync(
        long userId,
        long id,
        CancellationToken ct)
    {
        var note = await FindText(userId, id, ct)
            ?? throw new NotesException(
                "Không tìm thấy ghi chú của bạn.",
                404);

        return Map(note);
    }

    public async Task SaveTextAsync(
        long userId,
        EditTextNoteDto dto,
        CancellationToken ct)
    {
        var title = Title(dto.Title);

        if (dto.Body?.Length > 200000
            || dto.Format is not ("PlainText" or "Markdown"))
        {
            throw new NotesException(
                "Nội dung hoặc định dạng không hợp lệ.");
        }

        Note entity;

        if (dto.Id is long id)
        {
            entity = await FindText(userId, id, ct)
                ?? throw new NotesException(
                    "Không tìm thấy ghi chú.",
                    404);

            CheckVersion(entity, dto.Version);
        }
        else
        {
            entity = new Note
            {
                OwnerUserId = userId,
                Visibility = "Private",
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        entity.Title = title;
        entity.Body = dto.Body ?? "";
        entity.BodyFormat = dto.Format;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (dto.Id.HasValue)
        {
            repository.Update(entity);
        }
        else
        {
            await repository.AddAsync(entity, ct);
        }

        await Commit(ct);
    }

    public async Task DeleteTextAsync(
        long userId,
        long id,
        string version,
        CancellationToken ct)
    {
        var note = await FindText(userId, id, ct)
            ?? throw new NotesException(
                "Không tìm thấy ghi chú.",
                404);

        CheckVersion(note, version);

        note.IsDeleted = true;
        note.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(note);

        await Commit(ct);
    }

    public async Task TogglePinAsync(
        long userId,
        long id,
        CancellationToken ct)
    {
        var note = await FindText(userId, id, ct)
            ?? throw new NotesException(
                "Không tìm thấy ghi chú.",
                404);

        note.IsPinned = !note.IsPinned;
        note.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(note);

        await Commit(ct);
    }

    public async Task<long> UploadAsync(
        long userId,
        string name,
        Stream input,
        CancellationToken ct)
    {
        name = Path.GetFileName(name.Replace('\\', '/'));

        if (name.Length > 255
            || !name.EndsWith(
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotesException(
                "Chọn file PDF có tên tối đa 255 ký tự.");
        }

        var title = Title(Path.GetFileNameWithoutExtension(name));

        using var buffer = new MemoryStream();

        var chunk = new byte[81920];
        int length;

        while ((length = await input.ReadAsync(
            chunk.AsMemory(), ct)) > 0)
        {
            if (buffer.Length + length > MaxUpload)
            {
                throw new NotesException("PDF tối đa 20 MiB.");
            }

            await buffer.WriteAsync(
                chunk.AsMemory(0, length),
                ct);
        }

        if (buffer.Length < 5
            || !buffer.GetBuffer()
                .AsSpan(0, 5)
                .SequenceEqual("%PDF-"u8))
        {
            throw new NotesException(
                "File không có chữ ký PDF.");
        }

        buffer.Position = 0;

        var key = await files.AddPdfAsync(buffer, ct);

        var document = new Document
        {
            OwnerUserId = userId,
            Title = title,
            ResourceType = "File",
            StorageKey = Prefix + key,
            OriginalFileName = name,
            MimeType = "application/pdf",
            FileSizeBytes = buffer.Length,
            Visibility = "Private",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await repository.AddAsync(document, ct);
            await repository.SaveChangesAsync(ct);
        }
        catch
        {
            files.DeletePdf(key);
            throw;
        }

        return document.DocumentId;
    }

    public async Task<PdfNoteDto> GetPdfAsync(
        long userId,
        long id,
        CancellationToken ct)
    {
        var document = await FindPdf(userId, id, ct);

        var isPinned = await repository.AnyAsync<DocumentBookmark>(
            b => b.UserId == userId && b.DocumentId == id,
            ct);

        return new PdfNoteDto(
            document.DocumentId,
            document.Title,
            document.FileSizeBytes ?? 0,
            isPinned);
    }

    public async Task TogglePdfPinAsync(
        long userId,
        long id,
        CancellationToken ct)
    {
        // Xác nhận PDF tồn tại và thuộc người dùng hiện tại.
        _ = await FindPdf(userId, id, ct);

        var bookmark =
            await repository.FirstOrDefaultAsync<DocumentBookmark>(
                b => b.UserId == userId && b.DocumentId == id,
                ct);

        if (bookmark == null)
        {
            await repository.AddAsync(
                new DocumentBookmark
                {
                    UserId = userId,
                    DocumentId = id,
                    CreatedAtUtc = DateTime.UtcNow
                },
                ct);
        }
        else
        {
            repository.Remove(bookmark);
        }

        await Commit(ct);
    }

    public async Task RenamePdfAsync(
        long userId,
        long id,
        string title,
        CancellationToken ct)
    {
        var document = await FindPdf(userId, id, ct);

        document.Title = Title(title);
        document.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(document);

        await Commit(ct);
    }

    public async Task DeletePdfAsync(
        long userId,
        long id,
        CancellationToken ct)
    {
        var document = await FindPdf(userId, id, ct);
        var key = Key(document);

        document.IsDeleted = true;
        document.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(document);

        await Commit(ct);

        files.DeletePdf(key);
    }

    public async Task<Stream> OpenPdfAsync(
        long userId,
        long id,
        CancellationToken ct)
    {
        var document = await FindPdf(userId, id, ct);

        try
        {
            return files.OpenPdf(Key(document));
        }
        catch (IOException)
        {
            throw new NotesException(
                "Không đọc được file PDF trên server.",
                404);
        }
    }

    public async Task<PdfPageNoteDto> GetPageAsync(
        long userId,
        long id,
        int page,
        CancellationToken ct)
    {
        var document = await FindPdf(userId, id, ct);

        if (page is < 1 or > 10000)
        {
            throw new NotesException("Trang không hợp lệ.");
        }

        var stored = await files.ReadPageAsync(
            Key(document),
            page,
            ct);

        if (stored == null)
        {
            return new PdfPageNoteDto();
        }

        var dto = JsonSerializer.Deserialize<PdfPageNoteDto>(
            stored.Payload)
            ?? throw new NotesException(
                "Dữ liệu ghi chú bị hỏng.",
                500);

        dto.Version = stored.Version;

        return dto;
    }

    public async Task<PdfPageNoteDto> SavePageAsync(
        long userId,
        long id,
        int page,
        PdfPageNoteDto dto,
        CancellationToken ct)
    {
        var document = await FindPdf(userId, id, ct);

        if (page is < 1 or > 10000
            || dto.Text == null
            || dto.Text.Length > 20000
            || dto.Strokes == null
            || dto.Strokes.Count > 2000)
        {
            throw new NotesException(
                "Trang hoặc nội dung ghi chú không hợp lệ.");
        }

        long points = 0;

        foreach (var stroke in dto.Strokes)
        {
            if (stroke == null
                || stroke.Tool is not ("pen" or "highlight")
                || stroke.Color == null
                || !Regex.IsMatch(
                    stroke.Color,
                    "^#[0-9a-fA-F]{6}$")
                || !double.IsFinite(stroke.Width)
                || stroke.Width is < 1 or > 40
                || stroke.Points == null
                || stroke.Points.Count is < 1 or > 10000)
            {
                throw new NotesException(
                    "Nét vẽ không hợp lệ.");
            }

            points += stroke.Points.Count;

            if (points > 100000
                || stroke.Points.Any(p =>
                    p == null
                    || !double.IsFinite(p.X)
                    || !double.IsFinite(p.Y)
                    || p.X is < 0 or > 1
                    || p.Y is < 0 or > 1))
            {
                throw new NotesException(
                    "Tọa độ hoặc số điểm vẽ không hợp lệ.");
            }
        }

        dto.Version = await files.SavePageAsync(
            Key(document),
            page,
            dto.Version,
            JsonSerializer.Serialize(dto),
            ct)
            ?? throw new NotesException(
                "Trang đã được lưu ở tab khác. "
                + "Giữ lại nội dung cần thiết rồi tải lại.",
                409);

        return dto;
    }
}
