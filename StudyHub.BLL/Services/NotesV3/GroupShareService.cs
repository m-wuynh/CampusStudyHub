using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Auth;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.NotesV3.Sharing;

public sealed class GroupShareService(
    IRepository repository,
    ICurrentUser currentUser,
    INotesV3AccessService access) : IGroupShareService
{
    public async Task SharePdfAsync(
        long documentId, long groupId, CancellationToken ct = default)
    {
        await access.RequireOwnedPdfAsync(
            documentId, forWrite: true, cancellationToken: ct);
        await access.RequireGroupAsync(groupId, ct);

        var alreadyShared = await repository.Query<NotesV3DocumentShare>()
            .AnyAsync(x => x.DocumentId == documentId &&
                           x.StudyGroupId == groupId &&
                           x.IsActive, ct);

        if (alreadyShared)
        {
            throw new NotesV3Exception(409, "PDF đã được chia sẻ vào nhóm này.");
        }

        var share = new NotesV3DocumentShare
        {
            DocumentId = documentId,
            StudyGroupId = groupId,
            SharedByUserId = currentUser.UserId,
            IsActive = true,
            SharedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(share, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new NotesV3Exception(409, "PDF đã được chia sẻ vào nhóm này.");
        }
    }
}
