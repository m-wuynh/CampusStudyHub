using System.Data;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.Auth;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.StudyGroups;

namespace StudyHub.Pdf.Tests;

internal sealed class PdfTestContext : IAsyncDisposable
{
    public PdfTestContext(long userId = 1)
    {
        var options = new DbContextOptionsBuilder<StudyHubDbContext>()
            .UseInMemoryDatabase($"pdf-tests-{Guid.NewGuid():N}")
            .Options;

        Db = new StudyHubDbContext(options);
        Repository = new TestRepository(Db);
        CurrentUser = new TestCurrentUser(userId);
    }

    public StudyHubDbContext Db { get; }
    public TestRepository Repository { get; }
    public TestCurrentUser CurrentUser { get; }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();
}

internal sealed class TestCurrentUser(long userId) : ICurrentUser
{
    public long UserId { get; set; } = userId;
    public string DisplayName => "PDF tester";
}

internal sealed class TestRepository(StudyHubDbContext context) : IRepository
{
    public IGoogleAuthRepository GoogleAuth => null!;
    public IStudyGroupRepository StudyGroups => null!;
    public bool HasActiveTransaction { get; set; }
    public int BeginTransactionCalls { get; private set; }

    public IQueryable<TEntity> Query<TEntity>(bool asNoTracking = true)
        where TEntity : class
    {
        var query = context.Set<TEntity>().AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public ValueTask<TEntity?> FindAsync<TEntity>(
        object[] keyValues,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        context.Set<TEntity>().FindAsync(keyValues, cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync<TEntity>(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        Query<TEntity>().FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<List<TEntity>> ListAsync<TEntity>(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var query = Query<TEntity>();
        return (predicate is null ? query : query.Where(predicate))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnyAsync<TEntity>(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        Query<TEntity>().AnyAsync(predicate, cancellationToken);

    public Task<int> CountAsync<TEntity>(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var query = Query<TEntity>();
        return predicate is null
            ? query.CountAsync(cancellationToken)
            : query.CountAsync(predicate, cancellationToken);
    }

    public Task AddAsync<TEntity>(
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        context.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();

    public Task AddRangeAsync<TEntity>(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        context.Set<TEntity>().AddRangeAsync(entities, cancellationToken);

    public void Update<TEntity>(TEntity entity) where TEntity : class =>
        context.Set<TEntity>().Update(entity);

    public void UpdateRange<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class => context.Set<TEntity>().UpdateRange(entities);

    public void Remove<TEntity>(TEntity entity) where TEntity : class =>
        context.Set<TEntity>().Remove(entity);

    public void RemoveRange<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class => context.Set<TEntity>().RemoveRange(entities);

    public int SaveChanges() => context.SaveChanges();

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        BeginTransactionCalls++;
        return Task.FromResult<IDbContextTransaction>(new NoOpTransaction());
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        BeginTransactionCalls++;
        return Task.FromResult<IDbContextTransaction>(new NoOpTransaction());
    }
}

internal sealed class NoOpTransaction : IDbContextTransaction
{
    public Guid TransactionId { get; } = Guid.NewGuid();
    public bool SupportsSavepoints => false;

    public void Commit() { }
    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public void Rollback() { }
    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public void CreateSavepoint(string name) => throw new NotSupportedException();
    public Task CreateSavepointAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException());
    public void RollbackToSavepoint(string name) =>
        throw new NotSupportedException();
    public Task RollbackToSavepointAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException());
    public void ReleaseSavepoint(string name) => throw new NotSupportedException();
    public Task ReleaseSavepointAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException());
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakePdfStorage : IPdfFileStorage
{
    private readonly byte[] validPdf = PdfTestData.OnePagePdf();

    public int SaveCalls { get; private set; }
    public List<string> DeletedKeys { get; } = [];

    public async Task<StoredPdf> SaveAsync(
        Stream source,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        await source.CopyToAsync(Stream.Null, cancellationToken);
        return new StoredPdf("00000000000000000000000000000000.pdf", fileName, validPdf.Length);
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream(validPdf, writable: false));

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        DeletedKeys.Add(storageKey);
        return Task.CompletedTask;
    }

    public Task<int> DeleteOrphansAsync(
        IReadOnlyCollection<string> retainedStorageKeys,
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal static class PdfTestData
{
    public static byte[] OnePagePdf()
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>"
        };

        using var stream = new MemoryStream();
        Write(stream, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(stream.Position);
            Write(stream, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xref = stream.Position;
        Write(stream, $"xref\n0 {objects.Length + 1}\n");
        Write(stream, "0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            Write(stream, $"{offset:0000000000} 00000 n \n");
        }

        Write(stream,
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\n" +
            $"startxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static void Write(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes);
    }
}
