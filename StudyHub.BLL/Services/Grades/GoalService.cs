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
    Task<List<GoalLadderDto>> GetGoalLaddersAsync(long userId, long termId, CancellationToken cancellationToken = default);
    Task CreateGoalAsync(long userId, CreateGoalDto dto, CancellationToken cancellationToken = default);
    Task UpdateGoalAsync(long userId, UpdateGoalDto dto, CancellationToken cancellationToken = default);
    Task CancelGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default);
}

public sealed class GoalService(IRepository repository) : IGoalService
{
    public async Task<List<GoalLadderDto>> GetGoalLaddersAsync(long userId, long termId, CancellationToken cancellationToken = default)
    {
        // Load all UserSubjects in the term
        var userSubjects = await repository.Query<UserSubject>()
            .Include(us => us.Subject)
            .Where(us => us.UserId == userId && us.AcademicTermId == termId && !us.IsArchived)
            .ToListAsync(cancellationToken);

        if (!userSubjects.Any()) return new List<GoalLadderDto>();

        var subjectIds = userSubjects.Select(us => us.UserSubjectId).ToList();

        // Load all goals for these subjects
        var goals = await repository.Query<Goal>()
            .Where(g => g.UserId == userId && subjectIds.Contains(g.UserSubjectId))
            .OrderBy(g => g.TargetValue)
            .ToListAsync(cancellationToken);

        // Load grade averages from DB view
        var summaries = await repository.Query<VwSubjectGradeSummary>()
            .Where(v => v.UserId == userId && v.AcademicTermId == termId)
            .ToListAsync(cancellationToken);

        var ladders = new List<GoalLadderDto>();

        foreach (var us in userSubjects)
        {
            var subjectGoals = goals.Where(g => g.UserSubjectId == us.UserSubjectId).OrderBy(g => g.TargetValue).ToList();
            if (!subjectGoals.Any()) continue;

            var avg = summaries.FirstOrDefault(s => s.UserSubjectId == us.UserSubjectId)?.CurrentAverage10;
            var subjectName = us.CustomSubjectName ?? us.Subject?.SubjectName ?? "Môn học";

            var activeGoals = subjectGoals.Where(g => !g.IsCancelled).OrderBy(g => g.TargetValue).ToList();
            var inProgressGoal = activeGoals.FirstOrDefault(g => avg == null || g.TargetValue > avg.Value);

            var goalDtos = subjectGoals.Select(g =>
            {
                GoalStatus status;
                if (g.IsCancelled) status = GoalStatus.Cancelled;
                else if (avg == null) status = GoalStatus.NoData;
                else if (avg >= g.TargetValue) status = GoalStatus.Achieved;
                else if (inProgressGoal != null && g.GoalId == inProgressGoal.GoalId) status = GoalStatus.InProgress;
                else status = GoalStatus.Future;

                return new GoalDto(g.GoalId, g.Title, g.TargetValue, avg, g.EndDate, g.UserSubjectId, status);
            }).ToList();

            ladders.Add(new GoalLadderDto(us.UserSubjectId, subjectName, avg, goalDtos));
        }

        return ladders;
    }

    public async Task CreateGoalAsync(long userId, CreateGoalDto dto, CancellationToken cancellationToken = default)
    {
        // ── Validate UserSubject ───────────────────────────────────
        var userSubject = await repository.Query<UserSubject>()
            .FirstOrDefaultAsync(us => us.UserSubjectId == dto.UserSubjectId && us.UserId == userId, cancellationToken);

        if (userSubject is null) throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");
        if (userSubject.IsArchived) throw new ArgumentException("Không thể đặt mục tiêu cho môn đã lưu trữ.");

        // ── Validate TargetValue ───────────────────────────────────
        if (dto.TargetValue <= 0 || dto.TargetValue > 10)
            throw new ArgumentException("Điểm mục tiêu phải nằm trong khoảng (0, 10].");

        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Tên mục tiêu không được để trống.");

        // ── Goal Ladder uniqueness: no duplicate TargetValue for same subject ──
        var duplicate = await repository.AnyAsync<Goal>(
            g => g.UserId == userId && g.UserSubjectId == dto.UserSubjectId && g.TargetValue == dto.TargetValue && !g.IsCancelled,
            cancellationToken);
        if (duplicate) throw new ArgumentException($"Môn học này đã có mục tiêu {dto.TargetValue} đang hoạt động.");

        // ── Goal Ladder ascending recommendation ───────────────────
        // New goal should be > all existing active goals (soft validation – warn but allow)
        var maxExistingTarget = await repository.Query<Goal>()
            .Where(g => g.UserId == userId && g.UserSubjectId == dto.UserSubjectId && !g.IsCancelled)
            .MaxAsync(g => (decimal?)g.TargetValue, cancellationToken);

        if (maxExistingTarget.HasValue && dto.TargetValue < maxExistingTarget.Value)
        {
            throw new ArgumentException(
                $"Mục tiêu mới ({dto.TargetValue}) phải lớn hơn mục tiêu đang hoạt động cao nhất ({maxExistingTarget.Value}). " +
                "Để hạ mục tiêu, hãy hủy mục tiêu cũ trước rồi tạo mục tiêu mới.");
        }

        var goal = new Goal
        {
            UserId = userId,
            UserSubjectId = dto.UserSubjectId,
            Title = dto.Title.Trim(),
            TargetValue = dto.TargetValue,
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

        if (goal is null) throw new ArgumentException("Mục tiêu không tồn tại.");
        if (goal.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");
        if (goal.IsCancelled) throw new ArgumentException("Không thể chỉnh sửa mục tiêu đã hủy.");

        // IMPORTANT: TargetValue is IMMUTABLE – it's a historical milestone.
        // Only Title and EndDate can be updated.
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Tên mục tiêu không được để trống.");

        goal.Title = dto.Title.Trim();
        goal.EndDate = dto.EndDate;

        repository.Update(goal);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default)
    {
        var goal = await repository.Query<Goal>(false)
            .FirstOrDefaultAsync(g => g.GoalId == goalId, cancellationToken);

        if (goal is null) return;
        if (goal.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        goal.IsCancelled = true;
        repository.Update(goal);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
