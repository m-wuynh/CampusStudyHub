using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Constants;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Grades;

/// <summary>
/// Ghi trạng thái mục tiêu xuống DB sau mọi thao tác làm đổi ĐTB (POST). Trang GET chỉ đọc.
/// Đồng thời ghi hành trình (GoalProgressLogs): mốc "ĐTB thay đổi" khi ĐTB hiển thị của môn đổi,
/// mốc "Chuyển trạng thái" khi trạng thái mục tiêu đổi.
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

        var subjectIds = goals.Select(g => g.UserSubjectId).Distinct().ToList();
        var averages = await GetSubjectAveragesAsync(userId, subjectIds, cancellationToken);
        var lastLogged = await GetLastLoggedAveragesAsync(userId, subjectIds, cancellationToken);
        var now = DateTime.UtcNow;
        var changed = false;

        // Hành trình: một mốc cấp môn khi ĐTB hiển thị khác mốc ĐTB gần nhất đã ghi.
        foreach (var subjectId in subjectIds)
        {
            averages.TryGetValue(subjectId, out var avg);
            var hasPrevious = lastLogged.TryGetValue(subjectId, out var previous);
            if (hasPrevious && GradeCalculator.RoundForDisplay(previous) == GradeCalculator.RoundForDisplay(avg)) continue;

            await repository.AddAsync(GoalJourney.ScoreChanged(userId, subjectId, previous, avg, now), cancellationToken);
            changed = true;
        }

        foreach (var goal in goals)
        {
            averages.TryGetValue(goal.UserSubjectId, out var avg);
            var statusBefore = goal.Status;
            if (!GoalStatusEvaluator.ApplyTransition(goal, avg, now, out var history)) continue;
            if (history is not null) await repository.AddAsync(history, cancellationToken);
            if (goal.Status != statusBefore)
            {
                await repository.AddAsync(
                    GoalJourney.ForGoal(goal, GradeConstants.GoalJourneyEvent.StatusChanged, avg, statusBefore, now),
                    cancellationToken);
            }
            changed = true;
        }

        if (changed) await repository.SaveChangesAsync(cancellationToken);
    }

    public Task SyncSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default) =>
        SyncSubjectsAsync(userId, new[] { userSubjectId }, cancellationToken);

    /// <summary>
    /// ĐTB ở mốc gần nhất có ghi ĐTB của từng môn (mốc "ĐTB thay đổi" hoặc "Tạo mục tiêu").
    /// Môn chưa có mốc nào thì không có trong kết quả.
    /// </summary>
    private async Task<Dictionary<long, decimal?>> GetLastLoggedAveragesAsync(long userId, IReadOnlyCollection<long> userSubjectIds, CancellationToken cancellationToken)
    {
        var logs = await repository.Query<GoalProgressLog>()
            .Where(l => l.UserId == userId && userSubjectIds.Contains(l.UserSubjectId) &&
                        (l.EventType == GradeConstants.GoalJourneyEvent.ScoreChanged ||
                         l.EventType == GradeConstants.GoalJourneyEvent.GoalCreated))
            .Select(l => new { l.UserSubjectId, l.AverageScore, l.OccurredAtUtc, l.GoalProgressLogId })
            .ToListAsync(cancellationToken);

        return logs
            .GroupBy(l => l.UserSubjectId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(l => l.OccurredAtUtc).ThenByDescending(l => l.GoalProgressLogId).First().AverageScore);
    }

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
