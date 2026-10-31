using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;
using StudyHub.BLL.Services.NotesV3.References;
namespace StudyHub.BLL.Services.NotesV3.Collaboration;

public sealed class CollaborationService(
    IRepository repository,
    ICurrentUser currentUser,
    INotesV3AccessService access,
    IReferenceService references) : ICollaborationService
{
    private const int ReplayBatchSize = 32;

    // Giữ mọi update đã nhận, không tự xóa/compact.
    private const long MaxStoredBytesPerPage = 16L * 1024 * 1024;

    public async Task AuthorizePageAsync(
        long pageId,
        CancellationToken ct = default)
    {
        await RequireGroupPageAsync(
            pageId,
            forWrite: false,
            ct: ct);
    }

    public async Task<CollabAck> AppendAsync(
        CollabUpdateRequest request,
        CancellationToken ct = default)
    {
        ValidateUpdate(
            request.OperationId,
            request.UpdateBytes);

        // Kiểm tra trước khi chờ khóa.
        await RequireGroupPageAsync(
            request.PageId,
            forWrite: true,
            ct: ct);

        await using var transaction =
            await repository.BeginTransactionAsync(ct);

        await LockAsync(
            $"NotesV3:page:{request.PageId}",
            ct);

        // Không dựa vào quyền đã kiểm tra ở lúc join room.
        await RequireGroupPageAsync(
            request.PageId,
            forWrite: true,
            ct: ct);

        var ack = await PersistUpdateAsync(
    request.PageId,
    request.OperationId,
    request.UpdateBytes,
    request.Reference,
    ct);

        await transaction.CommitAsync(ct);

        // Chỉ trả ACK sau khi commit thành công.
        return ack;
    }

    public async Task<CollabAck?> AppendPdfPageAsync(
        PdfCollabUpdateRequest request,
        CancellationToken ct = default)
    {
        ValidateUpdate(
            request.OperationId,
            request.UpdateBytes);

        await RequirePdfPageAsync(request, ct);

        await using var transaction =
            await repository.BeginTransactionAsync(ct);

        // Khóa theo scope/version/trang trước khi PageId tồn tại.
        await LockAsync(
            $"NotesV3:pdf:{request.GroupDocumentShareId}:" +
            $"{request.DocumentVersionId}:{request.PhysicalPageNumber}",
            ct);

        await RequirePdfPageAsync(request, ct);

        var link = await repository.NotesV3.FindPdfPageLinkAsync(
            request.DocumentVersionId,
            request.PhysicalPageNumber,
            ownerUserId: null,
            groupDocumentShareId: request.GroupDocumentShareId,
            cancellationToken: ct);

        long pageId;

        if (link is null)
        {
            var initialDelta = NotesV3Validation.Delta(
                request.InitialDeltaJson);

            if (!initialDelta.HasMeaningfulText)
            {
                // Không tạo SQL row do điều hướng hoặc editor rỗng.
                await transaction.CommitAsync(ct);
                return null;
            }

            var page = new NotesV3Page
            {
                SectionId = null,
                Title = $"Trang {request.PhysicalPageNumber}",
                ContentDeltaJson = NotesV3Validation.EmptyDelta
            };

            await repository.AddAsync(page, ct);
            await repository.SaveChangesAsync(ct);

            pageId = page.PageId;

            await repository.AddAsync(
                new NotesV3PdfPageLink
                {
                    PageId = pageId,
                    DocumentVersionId =
                        request.DocumentVersionId,
                    OwnerUserId = null,
                    GroupDocumentShareId =
                        request.GroupDocumentShareId,
                    PhysicalPageNumber =
                        request.PhysicalPageNumber
                },
                ct);

            await repository.SaveChangesAsync(ct);
        }
        else
        {
            pageId = link.PageId;
        }

        // Cùng khóa mà AppendAsync dùng cho trang đã tồn tại.
        await LockAsync(
            $"NotesV3:page:{pageId}",
            ct);

        await RequireGroupPageAsync(
            pageId,
            forWrite: true,
            ct: ct);

        var ack = await PersistUpdateAsync(
    pageId,
    request.OperationId,
    request.UpdateBytes,
    request.Reference,
    ct);

        // Page, link và initial update cùng commit.
        await transaction.CommitAsync(ct);

        return ack;
    }

    public async Task<CollabBatch> ReadUpdatesAsync(
        long pageId,
        long afterUpdateId,
        CancellationToken ct = default)
    {
        if (afterUpdateId < 0)
        {
            throw new NotesV3Exception(
                400,
                "Cursor cập nhật không hợp lệ.");
        }

        // Mỗi lần catch-up đều kiểm tra lại quyền.
        await RequireGroupPageAsync(
            pageId,
            forWrite: false,
            ct: ct);

        var rows = await repository.NotesV3.ReadUpdatesAsync(
            pageId,
            afterUpdateId,
            ReplayBatchSize + 1,
            ct);

        var selected = rows
            .Take(ReplayBatchSize)
            .ToList();

        var updates = selected
            .Select(item => new CollabUpdateItem(
                item.UpdateId,
                item.OperationId,
                item.UpdateBytes))
            .ToList();

        var nextAfterId = selected.Count == 0
            ? afterUpdateId
            : selected[^1].UpdateId;

        return new CollabBatch(
            pageId,
            updates,
            nextAfterId,
            rows.Count > ReplayBatchSize);
    }

   private async Task<CollabAck> PersistUpdateAsync(
    long pageId,
    Guid operationId,
    byte[] updateBytes,
    CaptureReferenceInput? referenceInput,
    CancellationToken ct)
{
    var existing = await repository.NotesV3.FindOperationAsync(
        pageId,
        operationId,
        ct);

    if (existing is not null)
    {
        if (existing.ActorUserId != currentUser.UserId ||
            !existing.UpdateBytes.AsSpan()
                .SequenceEqual(updateBytes))
        {
            throw new NotesV3Exception(
                409,
                "OperationId đã được dùng cho cập nhật khác.");
        }

        // Retry không tạo thêm update hoặc reference.
        return new CollabAck(
            pageId,
            existing.UpdateId,
            operationId,
            AlreadyStored: true);
    }

    var storedBytes =
        await repository.NotesV3.GetStoredUpdateBytesAsync(
            pageId,
            ct);

    if (storedBytes + updateBytes.LongLength >
        MaxStoredBytesPerPage)
    {
        throw new NotesV3Exception(
            413,
            "Trang đã đạt giới hạn lưu cộng tác 16 MiB.");
    }

    if (referenceInput is not null)
    {
        var reference = await references.PrepareCaptureAsync(
            pageId,
            referenceInput,
            ct);

        await repository.AddAsync(reference, ct);
    }

    var update = new NotesV3CollabUpdate
    {
        PageId = pageId,
        OperationId = operationId,
        ActorUserId = currentUser.UserId,
        UpdateBytes = updateBytes,
        CreatedAtUtc = DateTime.UtcNow
    };

    await repository.AddAsync(update, ct);

    // Reference và update cùng nằm trong transaction
    // do AppendAsync/AppendPdfPageAsync quản lý.
    await repository.SaveChangesAsync(ct);

    return new CollabAck(
        pageId,
        update.UpdateId,
        operationId,
        AlreadyStored: false);
}

    private async Task RequireGroupPageAsync(
        long pageId,
        bool forWrite,
        CancellationToken ct)
    {
        var result = await access.RequirePageAsync(
            pageId,
            forWrite: forWrite,
            cancellationToken: ct);

        if (!result.IsGroup)
        {
            throw new NotesV3Exception(
                400,
                "Trang cá nhân không dùng cộng tác Yjs.");
        }
    }

    private async Task RequirePdfPageAsync(
        PdfCollabUpdateRequest request,
        CancellationToken ct)
    {
        if (request.GroupDocumentShareId <= 0 ||
            request.PhysicalPageNumber < 1)
        {
            throw new NotesV3Exception(
                400,
                "Scope hoặc số trang PDF không hợp lệ.");
        }

        await access.RequirePdfAsync(
            request.DocumentVersionId,
            request.GroupDocumentShareId,
            forWrite: true,
            cancellationToken: ct);

        var version = await repository
            .Query<NotesV3DocumentVersion>()
            .SingleOrDefaultAsync(
                item => item.DocumentVersionId ==
                        request.DocumentVersionId,
                ct)
            ?? throw new NotesV3Exception(
                404,
                "Không tìm thấy phiên bản PDF.");

        if (version.PageCount is not int count || count < 1)
        {
            throw new NotesV3Exception(
                409,
                "PDF chưa được xác định số trang.");
        }

        if (request.PhysicalPageNumber > count)
        {
            throw new NotesV3Exception(
                400,
                "Trang nằm ngoài tài liệu PDF.");
        }
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
                "Trang đang bận. Giữ cập nhật chưa đồng bộ và thử lại.");
        }
    }

    private static void ValidateUpdate(
        Guid operationId,
        byte[]? updateBytes)
    {
        if (operationId == Guid.Empty)
        {
            throw new NotesV3Exception(
                400,
                "Thiếu OperationId.");
        }

        if (updateBytes is null || updateBytes.Length == 0)
        {
            throw new NotesV3Exception(
                400,
                "Thiếu Yjs update.");
        }

        if (updateBytes.Length > NotesV3Validation.MaxUpdateBytes)
        {
            throw new NotesV3Exception(
                413,
                "Một Yjs update tối đa 256 KiB.");
        }
    }
}