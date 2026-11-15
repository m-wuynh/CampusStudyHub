using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.Annotations;

public sealed class PdfMarksService(
    IRepository repository,
    ICurrentUser currentUser,
    INotesV3AccessService access) : IPdfMarksService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PdfMarkDto>> ListAsync(
        long versionId, int pageNumber, long? shareId,
        CancellationToken ct = default)
    {
        await RequirePageAsync(versionId, pageNumber, shareId, false, ct);
        long? ownerId = shareId.HasValue ? null : currentUser.UserId;

        var rows = await repository.Query<NotesV3Annotation>()
            .Where(x => x.DocumentVersionId == versionId &&
                        x.PhysicalPageNumber == pageNumber &&
                        x.OwnerUserId == ownerId &&
                        x.GroupDocumentShareId == shareId &&
                        !x.IsDeleted)
            .OrderBy(x => x.AnnotationId)
            .ToListAsync(ct);

        var result = new List<PdfMarkDto>();
        foreach (var row in rows)
        {
            var mark = JsonSerializer.Deserialize<PdfMarkInput>(
                row.PayloadJson, JsonOptions);

            if (mark?.Kind is "Ink" or "Highlight" or "Comment")
            {
                result.Add(new PdfMarkDto(
                    row.AnnotationId,
                    mark,
                    Convert.ToBase64String(row.RowVersion)));
            }
        }

        return result;
    }

    public async Task CreateManyAsync(
        long versionId, int pageNumber, long? shareId,
        IReadOnlyList<PdfMarkInput> inputs, CancellationToken ct = default)
    {
        await RequirePageAsync(versionId, pageNumber, shareId, true, ct);
        if (inputs.Count is < 1 or > 100)
        {
            throw new NotesV3Exception(400, "Hãy chọn từ 1 đến 100 dấu trước khi lưu.");
        }

        // Kiểm tra toàn bộ trước khi ghi để một dấu sai không làm lưu dở cả nhóm.
        var validMarks = inputs.Select(Validate).ToList();
        foreach (var mark in validMarks)
        {
            await repository.AddAsync(new NotesV3Annotation
            {
                DocumentVersionId = versionId,
                PhysicalPageNumber = pageNumber,
                OwnerUserId = shareId.HasValue ? null : currentUser.UserId,
                GroupDocumentShareId = shareId,
                PayloadJson = JsonSerializer.Serialize(mark, JsonOptions)
            }, ct);
        }

        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        long markId, long versionId, int pageNumber, long? shareId,
        string versionToken, CancellationToken ct = default)
    {
        await RequirePageAsync(versionId, pageNumber, shareId, true, ct);
        long? ownerId = shareId.HasValue ? null : currentUser.UserId;

        var row = await repository.Query<NotesV3Annotation>(false)
            .SingleOrDefaultAsync(x => x.AnnotationId == markId &&
                                       x.DocumentVersionId == versionId &&
                                       x.PhysicalPageNumber == pageNumber &&
                                       x.OwnerUserId == ownerId &&
                                       x.GroupDocumentShareId == shareId &&
                                       !x.IsDeleted, ct)
            ?? throw new NotesV3Exception(404, "Không tìm thấy dấu đã chọn.");

        var expected = NotesV3Validation.Version(versionToken);
        if (!row.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new NotesV3Exception(409, "Dấu đã thay đổi. Hãy tải lại trang.");
        }

        row.IsDeleted = true;
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotesV3Exception(409, "Dấu đã thay đổi. Hãy tải lại trang.");
        }
    }

    private async Task RequirePageAsync(
        long versionId, int pageNumber, long? shareId,
        bool forWrite, CancellationToken ct)
    {
        await access.RequirePdfAsync(versionId, shareId, forWrite, ct);
        var pageCount = await repository.Query<NotesV3DocumentVersion>()
            .Where(x => x.DocumentVersionId == versionId)
            .Select(x => x.PageCount)
            .SingleAsync(ct);

        if (pageCount is null || pageNumber < 1 || pageNumber > pageCount)
        {
            throw new NotesV3Exception(400, "Trang PDF không hợp lệ.");
        }
    }

    private static PdfMarkInput Validate(PdfMarkInput input)
    {
        if (input is null)
        {
            throw new NotesV3Exception(400, "Dấu trên PDF không hợp lệ.");
        }

        if (input.Kind is not ("Ink" or "Highlight" or "Comment"))
        {
            throw new NotesV3Exception(400, "Kiểu đánh dấu không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(input.Color) ||
            !Regex.IsMatch(input.Color, "^#[0-9a-fA-F]{6}$"))
        {
            throw new NotesV3Exception(400, "Màu đánh dấu không hợp lệ.");
        }

        if (!InRange(input.X) || !InRange(input.Y) ||
            !InRange(input.Width) || !InRange(input.Height) ||
            input.Width <= 0 || input.Height <= 0 ||
            input.X + input.Width > 1.000001 ||
            input.Y + input.Height > 1.000001)
        {
            throw new NotesV3Exception(400, "Vị trí đánh dấu không hợp lệ.");
        }

        if (input.Kind == "Highlight")
        {
            return input with { Points = null, Text = null };
        }

        if (input.Kind == "Comment")
        {
            var text = input.Text?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > 2000)
            {
                throw new NotesV3Exception(400, "Comment cần có từ 1 đến 2000 ký tự.");
            }

            return input with { Points = null, Text = text };
        }

        if (input.Points is not { Count: >= 2 and <= 1500 } ||
            input.Points.Any(point => !InRange(point.X) || !InRange(point.Y)))
        {
            throw new NotesV3Exception(400, "Nét vẽ phải có từ 2 đến 1500 điểm.");
        }

        return input with { Text = null };
    }

    private static bool InRange(double value) =>
        double.IsFinite(value) && value >= 0 && value <= 1;
}
