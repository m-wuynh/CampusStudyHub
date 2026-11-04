using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Grades;

/// <summary>
/// Ghi trạng thái mục tiêu xuống DB sau mọi thao tác làm đổi ĐTB (POST). Trang GET chỉ đọc.
/// Người gọi chịu trách nhiệm bọc transaction cùng thao tác gốc.
/// </summary>
public sealed class GoalStatusSyncService(IRepository repository)
{
    /// <summary>ĐTB hiện tại của từng môn (môn không thuộc sổ hoặc chưa có điểm → null).</summary>
    public async Task<Dictionary<long, decimal?>> GetSubjectAveragesAsync(long userId, IReadOnlyCollection<long> userSubjectIds, CancellationToken cancellationToken = default)
    {
        var subjects = await repository.Query<UserSubject>()
            .Where(us => us.UserId == userId && userSubjectIds.Contains(us.UserSubjectId))
            .Select(us => new { us.UserSubjectId, us.GradeBookId })
            .ToListAsync(cancellationToken);

        var gradeBookIds = subjects.Where(s => s.GradeBookId.HasValue).Select(s => s.GradeBookId!.Value).Distinct().ToList();
        var columns = await repository.Query<GradeColumn>()
            .Where(c => gradeBookIds.Contains(c.GradeBookId))
            .ToListAsync(cancellationToken);
        var entries = await repository.Query<GradeEntry>()
            .Where(e => userSubjectIds.Contains(e.UserSubjectId) && !e.IsDeleted)
            .ToListAsync(cancellationToken);

        return subjects.ToDictionary(
            s => s.UserSubjectId,
            s => s.GradeBookId is null
                ? null
                : GradeCalculator.CalculateSubjectAverage(
                    entries.Where(e => e.UserSubjectId == s.UserSubjectId),
                    columns.Where(c => c.GradeBookId == s.GradeBookId.Value)));
    }

    public async Task<decimal?> GetSubjectAverageAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default)
    {
        var averages = await GetSubjectAveragesAsync(userId, new[] { userSubjectId }, cancellationToken);
        return averages.TryGetValue(userSubjectId, out var avg) ? avg : null;
    }

    public async Task SyncSubjectsAsync(long userId, IReadOnlyCollection<long> userSubjectIds, CancellationToken cancellationToken = default)
    {
        if (userSubjectIds.Count == 0) return;

        var goals = await repository.Query<Goal>(false)
            .Where(g => g.UserId == userId && userSubjectIds.Contains(g.UserSubjectId) && !g.IsCancelled)
            .ToListAsync(cancellationToken);
        if (goals.Count == 0) return;

        var averages = await GetSubjectAveragesAsync(userId, goals.Select(g => g.UserSubjectId).Distinct().ToList(), cancellationToken);
        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var goal in goals)
        {
            averages.TryGetValue(goal.UserSubjectId, out var avg);
            if (!GoalStatusEvaluator.ApplyTransition(goal, avg, now, out var history)) continue;
            if (history is not null) await repository.AddAsync(history, cancellationToken);
            changed = true;
        }

        if (changed) await repository.SaveChangesAsync(cancellationToken);
    }

    public Task SyncSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default) =>
        SyncSubjectsAsync(userId, new[] { userSubjectId }, cancellationToken);

    /// <summary>Đồng bộ mọi môn của một sổ (sau khi đổi hệ số hoặc xóa cột).</summary>
    public async Task SyncGradeBookAsync(long userId, long gradeBookId, CancellationToken cancellationToken = default)
    {
        var subjectIds = await repository.Query<UserSubject>()
            .Where(us => us.UserId == userId && us.GradeBookId == gradeBookId)
            .Select(us => us.UserSubjectId)
            .ToListAsync(cancellationToken);
        await SyncSubjectsAsync(userId, subjectIds, cancellationToken);
    }
}
