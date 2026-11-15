using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Pdf.Access;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Pdf.Annotations;

public sealed class PdfMarksService(
    IRepository repository,
    ICurrentUser currentUser,
    PdfAccessService access)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PdfMarkDto>> ListAsync(
        long documentId, int pageNumber, long? shareId,
        CancellationToken ct = default)
    {
        await access.RequirePdfPageAsync(
            documentId, pageNumber, shareId, cancellationToken: ct);
        // Chia sẻ nhóm chỉ cung cấp file PDF gốc; mọi annotation luôn là dữ liệu cá nhân.
        if (shareId.HasValue)
        {
            return [];
        }

        var rows = await repository.Query<PdfAnnotation>()
            .Where(x => x.DocumentId == documentId &&
                        x.PhysicalPageNumber == pageNumber &&
                        x.OwnerUserId == currentUser.UserId)
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

    public async Task<IReadOnlyList<PdfMarkDto>> CreateManyAsync(
        long documentId, int pageNumber, long? shareId,
        IReadOnlyList<PdfMarkInput> inputs, CancellationToken ct = default)
    {
        if (inputs.Count is < 1 or > 100)
        {
            throw new PdfException(400, "Hãy chọn từ 1 đến 100 dấu trước khi lưu.");
        }

        var validMarks = inputs.Select(Validate).ToList();
        try
        {
            await using var transaction = await repository.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);
            await access.RequirePdfPageAsync(
                documentId, pageNumber, shareId, forWrite: true, cancellationToken: ct);

            // Count and insert must be one serializable operation so concurrent
            // autosaves cannot both consume the same remaining quota.
            var existingCount = await repository.Query<PdfAnnotation>()
                .CountAsync(x => x.DocumentId == documentId &&
                                 x.PhysicalPageNumber == pageNumber &&
                                 x.OwnerUserId == currentUser.UserId, ct);
            if (existingCount > PdfPolicy.MaxAnnotationsPerPage - validMarks.Count)
            {
                throw new PdfException(
                    409,
                    $"Mỗi trang được lưu tối đa {PdfPolicy.MaxAnnotationsPerPage:N0} dấu.");
            }

            var rows = validMarks.Select(mark => new PdfAnnotation
            {
                DocumentId = documentId,
                PhysicalPageNumber = pageNumber,
                OwnerUserId = currentUser.UserId,
                PayloadJson = JsonSerializer.Serialize(mark, JsonOptions)
            }).ToList();

            await repository.AddRangeAsync(rows, ct);
            await repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return rows.Select((row, index) => new PdfMarkDto(
                row.AnnotationId,
                validMarks[index],
                Convert.ToBase64String(row.RowVersion))).ToList();
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            throw new PdfException(
                409,
                "Có một lượt lưu đánh dấu khác đang được xử lý. Hãy thử lại.");
        }
    }

    public async Task DeleteAsync(
        long markId, long documentId, int pageNumber, long? shareId,
        string versionToken, CancellationToken ct = default)
    {
        await access.RequirePdfPageAsync(
            documentId, pageNumber, shareId, forWrite: true, cancellationToken: ct);

        var row = await repository.Query<PdfAnnotation>(false)
            .SingleOrDefaultAsync(x => x.AnnotationId == markId &&
                                       x.DocumentId == documentId &&
                                       x.PhysicalPageNumber == pageNumber &&
                                       x.OwnerUserId == currentUser.UserId, ct)
            ?? throw new PdfException(404, "Không tìm thấy dấu đã chọn.");

        var expected = PdfValidation.Version(versionToken);
        if (!row.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new PdfException(409, "Dấu đã thay đổi. Hãy tải lại trang.");
        }

        repository.Remove(row);
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PdfException(409, "Dấu đã thay đổi. Hãy tải lại trang.");
        }
    }

    private static PdfMarkInput Validate(PdfMarkInput input)
    {
        if (input is null)
        {
            throw new PdfException(400, "Dấu trên PDF không hợp lệ.");
        }

        if (input.Kind is not ("Ink" or "Highlight" or "Comment"))
        {
            throw new PdfException(400, "Kiểu đánh dấu không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(input.Color) ||
            !Regex.IsMatch(input.Color, "^#[0-9a-fA-F]{6}$"))
        {
            throw new PdfException(400, "Màu đánh dấu không hợp lệ.");
        }

        if (!InRange(input.X) || !InRange(input.Y) ||
            !InRange(input.Width) || !InRange(input.Height) ||
            input.Width <= 0 || input.Height <= 0 ||
            input.X + input.Width > 1.000001 ||
            input.Y + input.Height > 1.000001)
        {
            throw new PdfException(400, "Vị trí đánh dấu không hợp lệ.");
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
                throw new PdfException(400, "Comment cần có từ 1 đến 2000 ký tự.");
            }

            return input with { Points = null, Text = text };
        }

        if (input.Points is not { Count: >= 2 and <= 1500 } ||
            input.Points.Any(point => !InRange(point.X) || !InRange(point.Y)))
        {
            throw new PdfException(400, "Nét vẽ phải có từ 2 đến 1500 điểm.");
        }

        return input with { Text = null };
    }

    private static bool InRange(double value) =>
        double.IsFinite(value) && value >= 0 && value <= 1;

    private static bool IsDeadlock(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
            {
                return true;
            }
        }

        return false;
    }
}
