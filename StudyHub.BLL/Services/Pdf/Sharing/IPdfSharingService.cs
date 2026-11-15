namespace StudyHub.BLL.Services.Pdf.Sharing;

public interface IPdfSharingService
{
    /// <summary>
    /// Chia sẻ một PDF vào nhóm học. Chỉ Owner của PDF mới được thực hiện.
    /// </summary>
    Task ShareAsync(
        long documentId,
        long groupId,
        long actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gỡ chia sẻ PDF khỏi nhóm học.
    /// Owner của PDF hoặc Group Manager đều có quyền thực hiện.
    /// </summary>
    Task UnshareAsync(
        long documentId,
        long groupId,
        long actorUserId,
        bool canManageGroup,
        CancellationToken cancellationToken = default);
}
