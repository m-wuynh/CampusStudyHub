using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.NotesV3;
using Microsoft.Data.SqlClient;
namespace StudyHub.DAL.Repositories.NotesV3;

public sealed class EfNotesV3Repository(
    StudyHubDbContext db) : INotesV3Repository
{
    public Task<NotesV3PdfPageLink?> FindPdfPageLinkAsync(
        long documentVersionId,
        int physicalPageNumber,
        long? ownerUserId,
        long? groupDocumentShareId,
        CancellationToken cancellationToken = default)
    {
        if (ownerUserId.HasValue == groupDocumentShareId.HasValue)
        {
            throw new ArgumentException(
                "Exactly one personal owner or group share is required.");
        }

        return db.NotesV3PdfPageLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.DocumentVersionId == documentVersionId
                     && x.PhysicalPageNumber == physicalPageNumber
                     && x.OwnerUserId == ownerUserId
                     && x.GroupDocumentShareId == groupDocumentShareId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<NotesV3CollabUpdate>> ReadUpdatesAsync(
        long pageId,
        long afterUpdateId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (afterUpdateId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(afterUpdateId));
        }

        if (take is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        return await db.NotesV3CollabUpdates
            .AsNoTracking()
            .Where(x => x.PageId == pageId && x.UpdateId > afterUpdateId)
            .OrderBy(x => x.UpdateId)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<NotesV3CollabUpdate?> FindOperationAsync(
        long pageId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        return db.NotesV3CollabUpdates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.PageId == pageId && x.OperationId == operationId,
                cancellationToken);
    }

    public async Task<long> GetStoredUpdateBytesAsync(
        long pageId,
        CancellationToken cancellationToken = default)
    {
        return await db.NotesV3CollabUpdates
            .Where(x => x.PageId == pageId)
            .Select(x => (long?)EF.Functions.DataLength(x.UpdateBytes))
            .SumAsync(cancellationToken) ?? 0L;
    }
    public async Task AcquireLockAsync(
    string resource,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(resource) ||
        resource.Length > 200 ||
        !resource.StartsWith(
            "NotesV3:",
            StringComparison.Ordinal))
    {
        throw new ArgumentException(
            "Invalid NotesV3 lock resource.",
            nameof(resource));
    }

    if (db.Database.CurrentTransaction is null)
    {
        throw new InvalidOperationException(
            "A transaction is required before acquiring the lock.");
    }

    var results = await db.Database.SqlQueryRaw<int>(
        """
        DECLARE @result int;

        EXEC @result = sys.sp_getapplock
            @Resource = @resource,
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = 5000;

        SELECT @result AS [Value];
        """,
        new SqlParameter("@resource", resource))
        .ToListAsync(cancellationToken);

    var result = results.Single();

    if (result < 0)
    {
        throw new TimeoutException(
            $"Could not acquire NotesV3 lock. Result: {result}.");
    }
}
}