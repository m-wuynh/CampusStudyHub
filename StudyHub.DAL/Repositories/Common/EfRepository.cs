using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.Auth;

namespace StudyHub.DAL.Repositories.Common;

public sealed class EfRepository(
    StudyHubDbContext context,
    IGoogleAuthRepository googleAuth) : IRepository
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (Exception exception) when (IsConflict(exception))
        {
            throw new DataConflictException(exception);
        }
    }

    private static bool IsConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException ||
                current is Microsoft.Data.SqlClient.SqlException { Number: 1205 or 2601 or 2627 })
                return true;
        }
        return false;
    }

    public IGoogleAuthRepository GoogleAuth { get; } = googleAuth;
    public IQueryable<TEntity> Query<TEntity>(bool asNoTracking = true) where TEntity : class
    {
        var query = context.Set<TEntity>().AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public ValueTask<TEntity?> FindAsync<TEntity>(
        object[] keyValues,
        CancellationToken cancellationToken = default) where TEntity : class =>
        context.Set<TEntity>().FindAsync(keyValues, cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync<TEntity>(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) where TEntity : class =>
        Query<TEntity>().FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<List<TEntity>> ListAsync<TEntity>(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TEntity : class
    {
        var query = Query<TEntity>();
        return (predicate is null ? query : query.Where(predicate)).ToListAsync(cancellationToken);
    }

    public Task<bool> AnyAsync<TEntity>(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) where TEntity : class =>
        Query<TEntity>().AnyAsync(predicate, cancellationToken);

    public Task<int> CountAsync<TEntity>(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TEntity : class
    {
        var query = Query<TEntity>();
        return predicate is null
            ? query.CountAsync(cancellationToken)
            : query.CountAsync(predicate, cancellationToken);
    }

    public Task AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class =>
        context.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();

    public Task AddRangeAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) where TEntity : class =>
        context.Set<TEntity>().AddRangeAsync(entities, cancellationToken);

    public void Update<TEntity>(TEntity entity) where TEntity : class => context.Set<TEntity>().Update(entity);

    public void UpdateRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class => context.Set<TEntity>().UpdateRange(entities);

    public void Remove<TEntity>(TEntity entity) where TEntity : class => context.Set<TEntity>().Remove(entity);

    public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class => context.Set<TEntity>().RemoveRange(entities);

    public int SaveChanges() => context.SaveChanges();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        context.Database.BeginTransactionAsync(cancellationToken);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default) =>
        context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
}
