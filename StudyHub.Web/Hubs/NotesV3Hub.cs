using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using StudyHub.BLL.DTOs.NotesV3;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.BLL.Services.NotesV3.Collaboration;
using StudyHub.BLL.Services.NotesV3.PageNotes;

namespace StudyHub.Web.Hubs;

[Authorize]
public sealed class NotesV3Hub(
    ICollaborationService collaboration,
    IPageNotesService pageNotes,
    IAntiforgery antiforgery,
    IOptions<AntiforgeryOptions> antiforgeryOptions,
    ILogger<NotesV3Hub> logger) : Hub
{
    public async Task JoinPage(long pageId)
    {
        await collaboration.AuthorizePageAsync(
            pageId,
            Context.ConnectionAborted);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            PageRoom(pageId),
            Context.ConnectionAborted);
    }

    public Task LeavePage(long pageId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            PageRoom(pageId),
            Context.ConnectionAborted);
    }

    public async Task JoinPdfPage(
        long documentVersionId,
        long shareId,
        int physicalPageNumber)
    {
        if (shareId <= 0)
        {
            throw new NotesV3Exception(
                400,
                "Thiếu share session của PDF nhóm.");
        }

        // GetAsync kiểm tra quyền và số trang, không tạo row.
        await pageNotes.GetAsync(
            documentVersionId,
            physicalPageNumber,
            shareId,
            Context.ConnectionAborted);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            PdfRoom(
                documentVersionId,
                shareId,
                physicalPageNumber),
            Context.ConnectionAborted);
    }

    public Task LeavePdfPage(
        long documentVersionId,
        long shareId,
        int physicalPageNumber)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            PdfRoom(
                documentVersionId,
                shareId,
                physicalPageNumber),
            Context.ConnectionAborted);
    }

    public async Task<CollabAck> AppendUpdate(
        CollabUpdateRequest request,
        string requestVerificationToken)
    {
        await ValidateWriteTokenAsync(requestVerificationToken);

        if (request is null)
        {
            throw new NotesV3Exception(
                400,
                "Thiếu yêu cầu cập nhật.");
        }

        // Service chỉ trả về sau SQL commit.
        var ack = await collaboration.AppendAsync(
            request,
            Context.ConnectionAborted);

        await NotifyChangedAsync(
            ack,
            new[] { PageRoom(ack.PageId) });

        return ack;
    }

    public async Task<CollabAck?> AppendPdfPageUpdate(
        PdfCollabUpdateRequest request,
        string requestVerificationToken)
    {
        await ValidateWriteTokenAsync(requestVerificationToken);

        if (request is null)
        {
            throw new NotesV3Exception(
                400,
                "Thiếu yêu cầu cập nhật.");
        }

        var ack = await collaboration.AppendPdfPageAsync(
            request,
            Context.ConnectionAborted);

        if (ack is null)
        {
            return null;
        }

        await NotifyChangedAsync(
            ack,
            new[]
            {
                PageRoom(ack.PageId),
                PdfRoom(
                    request.DocumentVersionId,
                    request.GroupDocumentShareId,
                    request.PhysicalPageNumber)
            });

        return ack;
    }

    private async Task NotifyChangedAsync(
        CollabAck ack,
        IReadOnlyList<string> rooms)
    {
        try
        {
            await Clients.Groups(rooms).SendAsync(
                "PageChanged",
                new
                {
                    pageId = ack.PageId,
                    updateId = ack.UpdateId
                },
                Context.ConnectionAborted);
        }
        catch (Exception exception)
        {
            // SQL đã commit. Lỗi thông báo không được biến
            // thao tác đã lưu thành lỗi nghiệp vụ.
            logger.LogWarning(
                exception,
                "Could not notify NotesV3 page {PageId}.",
                ack.PageId);
        }
    }

    private async Task ValidateWriteTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) ||
            token.Length > 4096)
        {
            throw new NotesV3Exception(
                400,
                "Thiếu token xác thực yêu cầu.");
        }

        var connectionContext = Context.GetHttpContext();

        if (connectionContext is null)
        {
            throw new NotesV3Exception(
                400,
                "Không xác định được phiên kết nối.");
        }

        var headerName = antiforgeryOptions.Value.HeaderName;

        if (string.IsNullOrWhiteSpace(headerName))
        {
            throw new InvalidOperationException(
                "Configure an antiforgery header for NotesV3.");
        }

        // Tạo context xác thực riêng, không sửa HTTP request
        // đang được transport SignalR sử dụng.
        var validationContext = new DefaultHttpContext
        {
            RequestServices = connectionContext.RequestServices,
            User = Context.User!
        };

        validationContext.Request.Method = HttpMethods.Post;
        validationContext.Request.Scheme =
            connectionContext.Request.Scheme;
        validationContext.Request.Host =
            connectionContext.Request.Host;
        validationContext.Request.PathBase =
            connectionContext.Request.PathBase;

        validationContext.Request.Headers["Cookie"] =
            connectionContext.Request.Headers["Cookie"];

        validationContext.Request.Headers[headerName] = token;

        try
        {
            await antiforgery.ValidateRequestAsync(
                validationContext);
        }
        catch (AntiforgeryValidationException)
        {
            throw new NotesV3Exception(
                400,
                "Phiên xác thực yêu cầu đã hết hạn. " +
                "Giữ bản nháp và tải lại trang.");
        }
    }

    private static string PageRoom(long pageId) =>
        $"notesv3:page:{pageId}";

    private static string PdfRoom(
        long documentVersionId,
        long shareId,
        int physicalPageNumber) =>
        $"notesv3:pdf:{shareId}:" +
        $"{documentVersionId}:{physicalPageNumber}";
}