using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Grades;

public interface IGoalService
{
    Task<List<GoalDto>> GetGoalsAsync(long userId, long? termId, CancellationToken cancellationToken = default);
    Task CreateGoalAsync(long userId, CreateGoalDto dto, CancellationToken cancellationToken = default);
    Task UpdateGoalAsync(long userId, UpdateGoalDto dto, CancellationToken cancellationToken = default);
    Task CancelGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default);
}

public sealed class GoalService(IRepository repository) : IGoalService
{
    public async Task<List<GoalDto>> GetGoalsAsync(long userId, long? termId, CancellationToken cancellationToken = default)
    {
        var query = repository.Query<Goal>()
            .Include(g => g.UserSubject)
            .ThenInclude(us => us!.Subject)
            .Where(g => g.UserId == userId && !g.IsCancelled);

        if (termId.HasValue)
        {
            query = query.Where(g => g.UserSubjectId == null || g.UserSubject!.AcademicTermId == termId.Value);
        }

        var subjectSummaries = termId.HasValue 
            ? await repository.Query<VwSubjectGradeSummary>().Where(v => v.UserId == userId && v.AcademicTermId == termId.Value).ToListAsync(cancellationToken)
            : new List<VwSubjectGradeSummary>();

        var termSummary = termId.HasValue
            ? await repository.Query<VwTermGradeSummary>().FirstOrDefaultAsync(v => v.UserId == userId && v.AcademicTermId == termId.Value, cancellationToken)
            : null;

        var goals = await query
            .OrderBy(g => g.EndDate)
            .ToListAsync(cancellationToken);

        return goals.Select(g => {
            decimal currentValue = 0;
            if (g.UserSubjectId.HasValue) {
                currentValue = subjectSummaries.FirstOrDefault(s => s.UserSubjectId == g.UserSubjectId)?.CurrentAverage10 ?? 0m;
            } else {
                currentValue = termSummary?.CurrentAverage10 ?? 0m;
            }
            return new GoalDto(
                g.GoalId,
                g.Title,
                g.UnitCode,
                g.TargetValue,
                currentValue,
                g.EndDate,
                g.UserSubjectId,
                g.UserSubject?.CustomSubjectName ?? g.UserSubject?.Subject?.SubjectName ?? "Chung",
                currentValue > 0 && currentValue >= g.TargetValue
            );
        }).ToList();
    }

    public async Task CreateGoalAsync(long userId, CreateGoalDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.UserSubjectId.HasValue)
        {
            var hasAccess = await repository.AnyAsync<UserSubject>(us => us.UserSubjectId == dto.UserSubjectId && us.UserId == userId, cancellationToken);
            if (!hasAccess) throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");

            var existing = await repository.AnyAsync<Goal>(g => g.UserId == userId && g.UserSubjectId == dto.UserSubjectId && !g.IsCancelled, cancellationToken);
            if (existing) throw new ArgumentException("Môn học này đã có mục tiêu đang hoạt động.");
        }
        else
        {
            var existing = await repository.AnyAsync<Goal>(g => g.UserId == userId && g.UserSubjectId == null && !g.IsCancelled, cancellationToken);
            if (existing) throw new ArgumentException("Mục tiêu ĐTB Toàn khóa đã tồn tại và đang hoạt động.");
        }

        var goal = new Goal
        {
            UserId = userId,
            Title = dto.Title,
            UserSubjectId = dto.UserSubjectId,
            UnitCode = dto.UnitCode,
            TargetValue = dto.TargetValue,
            CurrentValue = 0,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = dto.EndDate,
            IsCancelled = false
        };

        await repository.AddAsync(goal, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGoalAsync(long userId, UpdateGoalDto dto, CancellationToken cancellationToken = default)
    {
        var goal = await repository.Query<Goal>(false)
            .FirstOrDefaultAsync(g => g.GoalId == dto.Id, cancellationToken);

        if (goal == null) throw new ArgumentException("Mục tiêu không tồn tại.");
        if (goal.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        if (dto.UserSubjectId.HasValue && dto.UserSubjectId != goal.UserSubjectId)
        {
            var hasAccess = await repository.AnyAsync<UserSubject>(us => us.UserSubjectId == dto.UserSubjectId && us.UserId == userId, cancellationToken);
            if (!hasAccess) throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");

            var existing = await repository.AnyAsync<Goal>(g => g.UserId == userId && g.UserSubjectId == dto.UserSubjectId && !g.IsCancelled, cancellationToken);
            if (existing) throw new ArgumentException("Môn học này đã có mục tiêu đang hoạt động.");
        }
        else if (!dto.UserSubjectId.HasValue && goal.UserSubjectId.HasValue)
        {
            var existing = await repository.AnyAsync<Goal>(g => g.UserId == userId && g.UserSubjectId == null && !g.IsCancelled, cancellationToken);
            if (existing) throw new ArgumentException("Mục tiêu ĐTB Toàn khóa đã tồn tại và đang hoạt động.");
        }

        goal.Title = dto.Title;
        goal.UserSubjectId = dto.UserSubjectId;
        goal.UnitCode = dto.UnitCode;
        goal.TargetValue = dto.TargetValue;
        goal.EndDate = dto.EndDate;

        repository.Update(goal);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default)
    {
        var goal = await repository.Query<Goal>(false)
            .FirstOrDefaultAsync(g => g.GoalId == goalId, cancellationToken);

        if (goal == null) return;
        if (goal.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        goal.IsCancelled = true;
        repository.Update(goal);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
