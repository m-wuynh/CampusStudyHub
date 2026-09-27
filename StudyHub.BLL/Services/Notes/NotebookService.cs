using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Notes;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Notes;

public interface INotebookService
{
    Task<NotebookEditDto> GetAsync(
        long userId,
        long? id,
        Guid? pageId,
        CancellationToken ct);

    Task<NotebookSaveResult> SaveAsync(
        long userId,
        NotebookEditDto dto,
        string command,
        CancellationToken ct);
}

public sealed class NotebookService(IRepository repository)
    : INotebookService
{
    private const string Marker = "STUDYHUB_NOTEBOOK_V1\n";
    private const int MaxPages = 200;
    private const int MaxBookLength = 2_000_000;

    private async Task<Note> FindAsync(
        long userId,
        long id,
        CancellationToken ct)
    {
        return await repository.FirstOrDefaultAsync<Note>(
            n => n.NoteId == id
                 && n.OwnerUserId == userId
                 && !n.IsDeleted,
            ct)
            ?? throw new NotesException(
                "Không tìm thấy notebook của bạn.", 404);
    }

    private static List<NotebookPageDto> ReadPages(string body)
    {
        // Note cũ được mở thành notebook một trang.
        // Chưa ghi đè DB cho đến khi người dùng nhấn lưu.
        if (!body.StartsWith(Marker, StringComparison.Ordinal))
        {
            return
            [
                new NotebookPageDto
                {
                    Id = Guid.Empty,
                    Title = "Trang 1",
                    Body = body
                }
            ];
        }

        try
        {
            var pages = JsonSerializer.Deserialize<List<NotebookPageDto>>(
                body[Marker.Length..]);

            if (pages == null
                || pages.Count == 0
                || pages.Any(p =>
                    p == null || p.Title == null || p.Body == null)
                || pages.Select(p => p.Id).Distinct().Count() != pages.Count)
            {
                throw new NotesException(
                    "Dữ liệu notebook không hợp lệ.", 500);
            }

            return pages;
        }
        catch (JsonException)
        {
            throw new NotesException(
                "Không đọc được dữ liệu các trang notebook.", 500);
        }
    }

    public async Task<NotebookEditDto> GetAsync(
        long userId,
        long? id,
        Guid? pageId,
        CancellationToken ct)
    {
        if (!id.HasValue)
        {
            return new NotebookEditDto
            {
                Pages =
                [
                    new NotebookPageDto
                    {
                        Id = Guid.Empty,
                        Title = "Trang 1"
                    }
                ]
            };
        }

        var note = await FindAsync(userId, id.Value, ct);
        var pages = ReadPages(note.Body);

        var selected = pageId.HasValue
            ? pages.FirstOrDefault(p => p.Id == pageId.Value)
                ?? throw new NotesException("Không tìm thấy trang.", 404)
            : pages[0];

        return new NotebookEditDto
        {
            Id = note.NoteId,
            Title = note.Title,
            Format = note.BodyFormat,
            Version = Convert.ToBase64String(note.RowVersion),
            PageId = selected.Id,
            PageTitle = selected.Title,
            Body = selected.Body,
            Pages = pages
        };
    }

    public async Task<NotebookSaveResult> SaveAsync(
        long userId,
        NotebookEditDto dto,
        string command,
        CancellationToken ct)
    {
        var title = (dto.Title ?? "").Trim();
        var pageTitle = (dto.PageTitle ?? "").Trim();

        if (title.Length is < 1 or > 200
            || pageTitle.Length is < 1 or > 100
            || dto.Body?.Length > 200000
            || dto.Format is not ("PlainText" or "Markdown"))
        {
            throw new NotesException(
                "Tên cuốn, tên trang, nội dung hoặc định dạng không hợp lệ.");
        }

        Guid? destination = null;

        if (command.StartsWith("go:", StringComparison.Ordinal))
        {
            if (!Guid.TryParse(command[3..], out var target))
                throw new NotesException("Trang cần chuyển không hợp lệ.");

            destination = target;
        }
        else if (command is not ("save" or "add" or "delete"))
        {
            throw new NotesException("Thao tác không hợp lệ.");
        }

        Note note;
        List<NotebookPageDto> pages;

        if (dto.Id.HasValue)
        {
            note = await FindAsync(userId, dto.Id.Value, ct);

            if (dto.Version != Convert.ToBase64String(note.RowVersion))
            {
                throw new NotesException(
                    "Notebook đã thay đổi ở tab khác. "
                    + "Sao chép nội dung đang sửa trước khi tải lại.",
                    409);
            }

            pages = ReadPages(note.Body);
        }
        else
        {
            note = new Note
            {
                OwnerUserId = userId,
                Visibility = "Private",
                CreatedAtUtc = DateTime.UtcNow
            };

            pages =
            [
                new NotebookPageDto
                {
                    Id = Guid.Empty,
                    Title = "Trang 1"
                }
            ];
        }

        var index = pages.FindIndex(p => p.Id == dto.PageId);

        if (index < 0)
            throw new NotesException("Không tìm thấy trang đang sửa.", 404);

        var selectedId = dto.PageId;

        if (command == "delete")
        {
            if (pages.Count == 1)
            {
                throw new NotesException(
                    "Notebook phải còn ít nhất một trang.");
            }

            pages.RemoveAt(index);
            selectedId = pages[Math.Min(index, pages.Count - 1)].Id;
        }
        else
        {
            pages[index].Title = pageTitle;
            pages[index].Body = dto.Body ?? "";

            if (command == "add")
            {
                if (pages.Count >= MaxPages)
                {
                    throw new NotesException(
                        $"Mỗi notebook tối đa {MaxPages} trang.");
                }

                var next = new NotebookPageDto
                {
                    Id = Guid.NewGuid(),
                    Title = $"Trang {pages.Count + 1}"
                };

                pages.Add(next);
                selectedId = next.Id;
            }
            else if (destination.HasValue)
            {
                if (!pages.Any(p => p.Id == destination.Value))
                    throw new NotesException("Không tìm thấy trang cần mở.", 404);

                selectedId = destination.Value;
            }
        }

        var serialized = Marker + JsonSerializer.Serialize(pages);

        if (serialized.Length > MaxBookLength)
        {
            throw new NotesException(
                "Nội dung cả notebook vượt giới hạn 2 triệu ký tự lưu trữ.");
        }

        note.Title = title;
        note.BodyFormat = dto.Format;
        note.Body = serialized;
        note.UpdatedAtUtc = DateTime.UtcNow;

        if (dto.Id.HasValue)
            repository.Update(note);
        else
            await repository.AddAsync(note, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotesException(
                "Notebook vừa được cập nhật ở nơi khác. "
                + "Giữ lại nội dung đang sửa trước khi tải lại.",
                409);
        }

        return new NotebookSaveResult(note.NoteId, selectedId);
    }
}
