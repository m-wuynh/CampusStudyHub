using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.Annotations;

public sealed class AnnotationService(
    IRepository repository,
    ICurrentUser currentUser,
    INotesV3AccessService access) : IAnnotationService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PdfAnnotationItem>> ListAsync(
        long documentVersionId,
        int physicalPageNumber,
        long? groupDocumentShareId = null,
        CancellationToken ct = default)
    {
        await RequirePdfPageAsync(
            documentVersionId,
            physicalPageNumber,
            groupDocumentShareId,
            forWrite: false,
            ct: ct);

        long? ownerUserId = groupDocumentShareId.HasValue
            ? null
            : currentUser.UserId;

        var annotations = await repository
            .Query<NotesV3Annotation>()
            .Where(item =>
                item.DocumentVersionId == documentVersionId &&
                item.PhysicalPageNumber == physicalPageNumber &&
                item.GroupDocumentShareId == groupDocumentShareId &&
                item.OwnerUserId == ownerUserId &&
                !item.IsDeleted)
            .OrderBy(item => item.AnnotationId)
            .ToListAsync(ct);

        return annotations.Select(ToItem).ToList();
    }

    public async Task<PdfAnnotationItem> CreateAsync(
        long documentVersionId,
        int physicalPageNumber,
        PdfAnnotationPayload payload,
        long? groupDocumentShareId = null,
        CancellationToken ct = default)
    {
        var normalized = ValidatePayload(payload);

        await RequirePdfPageAsync(
            documentVersionId,
            physicalPageNumber,
            groupDocumentShareId,
            forWrite: true,
            ct: ct);

        var annotation = new NotesV3Annotation
        {
            DocumentVersionId = documentVersionId,
            PhysicalPageNumber = physicalPageNumber,
            GroupDocumentShareId = groupDocumentShareId,
            OwnerUserId = groupDocumentShareId.HasValue
                ? null
                : currentUser.UserId,
            PayloadJson = JsonSerializer.Serialize(
                normalized,
                JsonOptions)
        };

        await repository.AddAsync(annotation, ct);
        await CommitAsync(ct);

        return ToItem(annotation);
    }

    public async Task<PdfAnnotationItem> UpdateAsync(
        long annotationId,
        PdfAnnotationPayload payload,
        string versionToken,
        CancellationToken ct = default)
    {
        var normalized = ValidatePayload(payload);

        var annotation = await RequireWritableAnnotationAsync(
            annotationId,
            versionToken,
            ct);

        annotation.PayloadJson = JsonSerializer.Serialize(
            normalized,
            JsonOptions);

        await CommitAsync(ct);

        return ToItem(annotation);
    }

    public async Task DeleteAsync(
        long annotationId,
        string versionToken,
        CancellationToken ct = default)
    {
        var annotation = await RequireWritableAnnotationAsync(
            annotationId,
            versionToken,
            ct);

        annotation.IsDeleted = true;

        await CommitAsync(ct);
    }

    private async Task<NotesV3Annotation>
        RequireWritableAnnotationAsync(
            long annotationId,
            string versionToken,
            CancellationToken ct)
    {
        var expected = NotesV3Validation.Version(versionToken);

        var annotation = await repository
            .Query<NotesV3Annotation>(asNoTracking: false)
            .SingleOrDefaultAsync(
                item => item.AnnotationId == annotationId &&
                        !item.IsDeleted,
                ct)
            ?? throw Missing();

        // Quyền đọc PDF cá nhân không đồng nghĩa được sửa
        // annotation cá nhân của người khác.
        if (annotation.GroupDocumentShareId is null &&
            annotation.OwnerUserId != currentUser.UserId)
        {
            throw Missing();
        }

        await RequirePdfPageAsync(
            annotation.DocumentVersionId,
            annotation.PhysicalPageNumber,
            annotation.GroupDocumentShareId,
            forWrite: true,
            ct: ct);

        if (!annotation.RowVersion.AsSpan()
                .SequenceEqual(expected))
        {
            throw Conflict();
        }

        return annotation;
    }

    private async Task RequirePdfPageAsync(
        long documentVersionId,
        int physicalPageNumber,
        long? groupDocumentShareId,
        bool forWrite,
        CancellationToken ct)
    {
        await access.RequirePdfAsync(
            documentVersionId,
            groupDocumentShareId,
            forWrite,
            ct);

        var version = await repository
            .Query<NotesV3DocumentVersion>()
            .SingleOrDefaultAsync(
                item =>
                    item.DocumentVersionId == documentVersionId,
                ct)
            ?? throw Missing();

        if (version.PageCount is not int count || count < 1)
        {
            throw new NotesV3Exception(
                409,
                "PDF chưa được xác định số trang.");
        }

        if (physicalPageNumber < 1 || physicalPageNumber > count)
        {
            throw new NotesV3Exception(
                400,
                "Trang nằm ngoài tài liệu PDF.");
        }
    }

    private static PdfAnnotationPayload ValidatePayload(
        PdfAnnotationPayload? payload)
    {
        if (payload is null)
        {
            throw new NotesV3Exception(
                400, "Thiếu nội dung annotation.");
        }

        if (payload.Kind is not ("Highlight" or "Comment"))
        {
            throw new NotesV3Exception(
                400,
                "Chỉ hỗ trợ Highlight hoặc Comment.");
        }

        if (!double.IsFinite(payload.X) ||
            !double.IsFinite(payload.Y) ||
            !double.IsFinite(payload.Width) ||
            !double.IsFinite(payload.Height) ||
            payload.X < 0 ||
            payload.Y < 0 ||
            payload.Width <= 0 ||
            payload.Height <= 0 ||
            payload.X + payload.Width > 1 ||
            payload.Y + payload.Height > 1)
        {
            throw new NotesV3Exception(
                400,
                "Tọa độ annotation phải hữu hạn và nằm trong trang.");
        }

        var text = payload.Text?.Trim();

        if (text?.Length > 2000)
        {
            throw new NotesV3Exception(
                400,
                "Nội dung annotation tối đa 2000 ký tự.");
        }

        if (payload.Kind == "Comment" &&
            string.IsNullOrWhiteSpace(text))
        {
            throw new NotesV3Exception(
                400,
                "Bình luận không được để trống.");
        }

        var color = payload.Color;

        if (string.IsNullOrWhiteSpace(color) ||
            color.Length != 7 ||
            color[0] != '#' ||
            !color.Skip(1).All(char.IsAsciiHexDigit))
        {
            throw new NotesV3Exception(
                400,
                "Màu phải có dạng #RRGGBB.");
        }

        return payload with
        {
            Text = string.IsNullOrWhiteSpace(text) ? null : text,
            Color = color.ToUpperInvariant()
        };
    }

    private async Task CommitAsync(CancellationToken ct)
    {
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict();
        }
    }

    private static PdfAnnotationItem ToItem(
        NotesV3Annotation annotation)
    {
        var payload =
            JsonSerializer.Deserialize<PdfAnnotationPayload>(
                annotation.PayloadJson,
                JsonOptions)
            ?? throw new InvalidOperationException(
                "Stored annotation payload is invalid.");

        return new PdfAnnotationItem(
            annotation.AnnotationId,
            annotation.PhysicalPageNumber,
            payload,
            Convert.ToBase64String(annotation.RowVersion));
    }

    private static NotesV3Exception Missing() =>
        new(404, "Không tìm thấy annotation hoặc tài liệu.");

    private static NotesV3Exception Conflict() =>
        new(
            409,
            "Annotation đã thay đổi ở nơi khác. Tải lại trước khi sửa.");
}