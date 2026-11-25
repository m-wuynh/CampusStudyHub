using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.Auth;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.Calendar.Tests;

internal sealed class CalendarTestContext : IAsyncDisposable
{
    public CalendarTestContext(long userId = 4)
    {
        var options = new DbContextOptionsBuilder<StudyHubDbContext>()
            .UseInMemoryDatabase($"calendar-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Db = new StudyHubDbContext(options);

        // Đảm bảo các thuộc tính bắt buộc của EF Core InMemory luôn có giá trị mặc định hợp lệ
        Db.SavingChanges += (sender, args) =>
        {
            foreach (var entry in Db.ChangeTracker.Entries<ScheduleEvent>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.RowVersion ??= [0, 0, 0, 0, 0, 0, 0, 1];
                    if (string.IsNullOrWhiteSpace(entry.Entity.TimeZoneId))
                    {
                        entry.Entity.TimeZoneId = "SE Asia Standard Time";
                    }
                    if (string.IsNullOrWhiteSpace(entry.Entity.RepeatMode))
                    {
                        entry.Entity.RepeatMode = "None";
                    }
                    if (string.IsNullOrWhiteSpace(entry.Entity.EventType))
                    {
                        entry.Entity.EventType = "Tự học";
                    }
                }
            }
        };

        Repository = new CalendarTestRepository(Db);
        UserId = userId;
    }

    public StudyHubDbContext Db { get; }
    public CalendarTestRepository Repository { get; }
    public long UserId { get; }

    public static ScheduleEvent CreateEvent(
        long userId,
        string title,
        string eventType,
        DateTime startAt,
        DateTime endAt,
        bool isCompleted = false,
        bool isDeleted = false)
    {
        return new ScheduleEvent
        {
            UserId = userId,
            Title = title,
            EventType = eventType,
            StartAtLocal = startAt,
            EndAtLocal = endAt,
            TimeZoneId = "SE Asia Standard Time",
            RepeatMode = "None",
            IsCompleted = isCompleted,
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            RowVersion = [0, 0, 0, 0, 0, 0, 0, 1]
        };
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();
}

internal sealed class CalendarTestRepository(StudyHubDbContext context) : IRepository
{
    public IGoogleAuthRepository GoogleAuth => null!;
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation) => operation();
    public bool HasActiveTransaction { get; set; }

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
        Query<TEntity>(false).FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<List<TEntity>> ListAsync<TEntity>(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var query = Query<TEntity>(false);
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
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IDbContextTransaction>(new NoOpTransaction());

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IDbContextTransaction>(new NoOpTransaction());
}

internal sealed class NoOpTransaction : IDbContextTransaction
{
    public Guid TransactionId { get; } = Guid.NewGuid();
    public bool SupportsSavepoints => false;

    public void Commit() { }
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Rollback() { }
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void CreateSavepoint(string name) => throw new NotSupportedException();
    public Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void RollbackToSavepoint(string name) => throw new NotSupportedException();
    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void ReleaseSavepoint(string name) => throw new NotSupportedException();
    public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
