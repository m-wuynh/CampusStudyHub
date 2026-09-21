using System.Linq.Expressions;
using StudyHub.DAL.Repositories.StudyGroups;

namespace StudyHub.DAL.Repositories.Common;

/// <summary>
/// Điểm truy cập duy nhất từ BLL tới tầng repository của DAL.
/// </summary>
public interface IRepository
{
    IStudyGroupRepository StudyGroups { get; }

    IQueryable<TEntity> Query<TEntity>(bool asNoTracking = true) where TEntity : class;
    ValueTask<TEntity?> FindAsync<TEntity>(
        object[] keyValues,
        CancellationToken cancellationToken = default) where TEntity : class;
    Task<TEntity?> FirstOrDefaultAsync<TEntity>(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) where TEntity : class;
    Task<List<TEntity>> ListAsync<TEntity>(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TEntity : class;
    Task<bool> AnyAsync<TEntity>(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) where TEntity : class;
    Task<int> CountAsync<TEntity>(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TEntity : class;
    Task AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class;
    Task AddRangeAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) where TEntity : class;
    void Update<TEntity>(TEntity entity) where TEntity : class;
    void UpdateRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;
    void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
    int SaveChanges();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
