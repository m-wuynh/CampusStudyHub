using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.References;

public sealed class ReferenceService(
    IRepository repository,
    INotesV3AccessService access,
    ICurrentUser currentUser) : IReferenceService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<PersonalCaptureResult> CapturePersonalAsync(
        long pageId,
        PersonalCaptureRequest request,
        CancellationToken ct = default)
    {
        var target = await access.RequirePageAsync(
            pageId,
            forWrite: true,
            cancellationToken: ct);

        if (target.IsGroup)
        {
            throw new NotesV3Exception(
                400,
                "Trang nhóm phải lưu capture cùng Yjs update.");
        }

        var delta = ValidateCaptureDelta(request);

        await using var transaction =
            await repository.BeginTransactionAsync(ct);

        await LockAsync($"NotesV3:page:{pageId}", ct);

        var reference = await PrepareCaptureAsync(
            pageId,
            request.Source,
            ct);

        var page = await TrackedPageAsync(pageId, ct);

        RequireVersion(page.RowVersion, request.VersionToken);

        page.ContentDeltaJson = delta.Json;

        await repository.AddAsync(reference, ct);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);

        return Result(reference, page);
    }

    public async Task<PersonalCaptureResult> CapturePersonalPdfAsync(
        long documentVersionId,
        int physicalPageNumber,
        PersonalCaptureRequest request,
        CancellationToken ct = default)
    {
        var delta = ValidateCaptureDelta(request);

        await access.RequirePdfAsync(
            documentVersionId,
            forWrite: true,
            cancellationToken: ct);

        await RequirePhysicalPageAsync(
            documentVersionId,
            physicalPageNumber,
            ct);

        await using var transaction =
            await repository.BeginTransactionAsync(ct);

        await LockAsync(
            $"NotesV3:personal-pdf:{currentUser.UserId}:" +
            $"{documentVersionId}:{physicalPageNumber}",
            ct);

        await access.RequirePdfAsync(
            documentVersionId,
            forWrite: true,
            cancellationToken: ct);

        try
        {
            var link = await repository.NotesV3.FindPdfPageLinkAsync(
                documentVersionId,
                physicalPageNumber,
                currentUser.UserId,
                groupDocumentShareId: null,
                cancellationToken: ct);

            NotesV3Page page;

            if (link is null)
            {
                if (!string.IsNullOrWhiteSpace(request.VersionToken))
                {
                    throw Conflict();
                }

                page = new NotesV3Page
                {
                    SectionId = null,
                    Title = $"Trang {physicalPageNumber}",
                    ContentDeltaJson = delta.Json
                };

                await repository.AddAsync(page, ct);
                await repository.SaveChangesAsync(ct);

                await repository.AddAsync(
                    new NotesV3PdfPageLink
                    {
                        PageId = page.PageId,
                        DocumentVersionId = documentVersionId,
                        OwnerUserId = currentUser.UserId,
                        GroupDocumentShareId = null,
                        PhysicalPageNumber = physicalPageNumber
                    },
                    ct);

                // AccessService cần đọc được link này.
                // Transaction vẫn chưa commit.
                await repository.SaveChangesAsync(ct);
            }
            else
            {
                await LockAsync(
                    $"NotesV3:page:{link.PageId}",
                    ct);

                page = await TrackedPageAsync(link.PageId, ct);

                RequireVersion(
                    page.RowVersion,
                    request.VersionToken);

                page.ContentDeltaJson = delta.Json;
            }

            var reference = await PrepareCaptureAsync(
                page.PageId,
                request.Source,
                ct);

            await repository.AddAsync(reference, ct);
            await SaveAsync(ct);

            // Page, link và reference cùng commit.
            await transaction.CommitAsync(ct);

            return Result(reference, page);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException
            {
                Number: 2601 or 2627
            })
        {
            throw Conflict();
        }
    }

    public async Task<IReadOnlyList<ReferenceItem>> ListAsync(
        long pageId,
        CancellationToken ct = default)
    {
        await access.RequirePageAsync(
            pageId,
            cancellationToken: ct);

        return await repository
            .Query<NotesV3Reference>()
            .Where(item => item.PageId == pageId)
            .OrderBy(item => item.ReferenceId)
            .Select(item => new ReferenceItem(
                item.ReferenceId,
                item.PhysicalPageNumber,
                item.CapturedText))
            .ToListAsync(ct);
    }

    public async Task<ReferenceDestination> ResolveAsync(
        long referenceId,
        CancellationToken ct = default)
    {
        var reference = await repository
            .Query<NotesV3Reference>()
            .SingleOrDefaultAsync(
                item => item.ReferenceId == referenceId,
                ct)
            ?? throw Missing();

        await access.RequirePageAsync(
            reference.PageId,
            cancellationToken: ct);

        try
        {
            var document = await access.RequirePdfAsync(
                reference.DocumentVersionId,
                reference.GroupDocumentShareId,
                cancellationToken: ct);

            return new ReferenceDestination(
                true,
                reference.DocumentVersionId,
                reference.PhysicalPageNumber,
                reference.GroupDocumentShareId,
                document.Title,
                document.DocumentId);
        }
        catch (NotesV3Exception exception)
            when (exception.StatusCode is 403 or 404)
        {
            return new ReferenceDestination(
                false,
                null,
                null,
                null,
                null);
        }
    }

    public async Task<NotesV3Reference> PrepareCaptureAsync(
        long pageId,
        CaptureReferenceInput input,
        CancellationToken ct = default)
    {
        var text = CaptureText(input);

        ValidateAnchor(input.Anchor);

        await access.RequirePageAsync(
            pageId,
            forWrite: true,
            cancellationToken: ct);

        await access.RequirePdfAsync(
            input.DocumentVersionId,
            input.GroupDocumentShareId,
            forWrite: true,
            cancellationToken: ct);

        await RequirePhysicalPageAsync(
            input.DocumentVersionId,
            input.PhysicalPageNumber,
            ct);

        return new NotesV3Reference
        {
            PageId = pageId,
            DocumentVersionId = input.DocumentVersionId,
            GroupDocumentShareId = input.GroupDocumentShareId,
            PhysicalPageNumber = input.PhysicalPageNumber,
            CapturedText = text,
            AnchorJson = input.Anchor is null
                ? null
                : JsonSerializer.Serialize(
                    input.Anchor,
                    JsonOptions)
        };
    }

    private async Task RequirePhysicalPageAsync(
        long versionId,
        int pageNumber,
        CancellationToken ct)
    {
        var version = await repository
            .Query<NotesV3DocumentVersion>()
            .SingleOrDefaultAsync(
                item => item.DocumentVersionId == versionId,
                ct)
            ?? throw Missing();

        if (version.PageCount is not int count || count < 1)
        {
            throw new NotesV3Exception(
                409,
                "PDF chưa được xác định số trang.");
        }

        if (pageNumber < 1 || pageNumber > count)
        {
            throw new NotesV3Exception(
                400,
                "Trang nguồn nằm ngoài PDF.");
        }
    }

    private async Task<NotesV3Page> TrackedPageAsync(
        long pageId,
        CancellationToken ct)
    {
        return await repository
            .Query<NotesV3Page>(asNoTracking: false)
            .SingleOrDefaultAsync(
                item => item.PageId == pageId && !item.IsDeleted,
                ct)
            ?? throw Missing();
    }

    private async Task LockAsync(
        string resource,
        CancellationToken ct)
    {
        try
        {
            await repository.NotesV3.AcquireLockAsync(
                resource,
                ct);
        }
        catch (TimeoutException)
        {
            throw new NotesV3Exception(
                409,
                "Trang đang bận. Giữ bản nháp và thử lại.");
        }
    }

    private async Task SaveAsync(CancellationToken ct)
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

    private static ValidatedDelta ValidateCaptureDelta(
        PersonalCaptureRequest request)
    {
        if (request is null)
        {
            throw new NotesV3Exception(
                400,
                "Thiếu yêu cầu capture.");
        }

        var text = CaptureText(request.Source);
        var delta = NotesV3Validation.Delta(request.DeltaJson);

        if (!delta.PlainText.Contains(
                text,
                StringComparison.Ordinal))
        {
            throw new NotesV3Exception(
                400,
                "Nội dung ghi chú chưa chứa đoạn văn bản được chụp.");
        }

        return delta;
    }

    private static string CaptureText(
        CaptureReferenceInput? input)
    {
        var text = input?.CapturedText?.Trim();

        if (string.IsNullOrWhiteSpace(text) ||
            text.Length > 4000)
        {
            throw new NotesV3Exception(
                400,
                "Đoạn capture phải có từ 1 đến 4000 ký tự.");
        }

        return text;
    }

    private static void RequireVersion(
        byte[] current,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw Conflict();
        }

        var expected = NotesV3Validation.Version(token);

        if (!current.AsSpan().SequenceEqual(expected))
        {
            throw Conflict();
        }
    }

    private static void ValidateAnchor(ReferenceAnchor? anchor)
    {
        if (anchor is null) return;

        if (!double.IsFinite(anchor.X) ||
            !double.IsFinite(anchor.Y) ||
            !double.IsFinite(anchor.Width) ||
            !double.IsFinite(anchor.Height) ||
            anchor.X < 0 ||
            anchor.Y < 0 ||
            anchor.Width <= 0 ||
            anchor.Height <= 0 ||
            anchor.X + anchor.Width > 1 ||
            anchor.Y + anchor.Height > 1)
        {
            throw new NotesV3Exception(
                400,
                "Vùng nguồn phải nằm trong trang PDF.");
        }
    }

    private static PersonalCaptureResult Result(
        NotesV3Reference reference,
        NotesV3Page page) =>
        new(
            reference.ReferenceId,
            Convert.ToBase64String(page.RowVersion),
            page.PageId);

    private static NotesV3Exception Missing() =>
        new(
            404,
            "Không tìm thấy ghi chú hoặc nguồn tham chiếu.");

    private static NotesV3Exception Conflict() =>
        new(
            409,
            "Ghi chú đã thay đổi. Giữ bản nháp và tải lại trước khi lưu.");
}