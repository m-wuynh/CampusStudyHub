using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Constants;
using StudyHub.BLL.DTOs;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Grades;

public sealed class GoalService(IRepository repository, GoalStatusSyncService goalStatusSync) : IGoalService
{
    public async Task CreateGoalAsync(long userId, CreateGoalDto dto, CancellationToken cancellationToken = default)
    {
        var userSubject = await repository.Query<UserSubject>()
            .FirstOrDefaultAsync(us => us.UserSubjectId == dto.UserSubjectId && us.UserId == userId, cancellationToken);

        if (userSubject is null) throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");
        if (userSubject.IsArchived) throw new ArgumentException("Không thể đặt mục tiêu cho môn đã lưu trữ.");
        if (userSubject.GradeBookId is null) throw new InvalidOperationException("Môn học chưa thuộc sổ điểm nào nên không thể tạo mục tiêu mới.");

        GradeGuard.EnsureTarget(dto.TargetValue);
        var title = GradeGuard.RequireText(dto.Title, GradeConstants.GoalTitleMaxLength, "Tên mục tiêu");
        EnsureEndDateNotPast(dto.EndDate);

        // Ladder = các mục tiêu chưa hủy của đúng môn này (không gộp môn theo tên).
        var existingGoals = await repository.Query<Goal>(false)
            .Where(g => g.UserId == userId && g.UserSubjectId == userSubject.UserSubjectId && !g.IsCancelled)
            .OrderBy(g => g.CreatedAtUtc)
            .ThenBy(g => g.TargetValue)
            .ToListAsync(cancellationToken);

        if (existingGoals.Any(g => g.TargetValue == dto.TargetValue))
            throw new ArgumentException($"Môn học này đã có mục tiêu {dto.TargetValue:0.0} đang hoạt động.");

        var now = DateTime.UtcNow;

        // Chuẩn hóa mục tiêu gốc trong POST: dữ liệu cũ chưa có gốc → mục tiêu tạo sớm nhất là gốc.
        var baseGoal = existingGoals.FirstOrDefault(g => g.IsBaseGoal);
        if (baseGoal == null && existingGoals.Any())
        {
            baseGoal = existingGoals.First();
            baseGoal.IsBaseGoal = true;
            baseGoal.UpdatedAtUtc = now;
        }

        // Mục tiêu đầu tiên là gốc; các mục tiêu sau là nấc bổ sung, phải lớn hơn mục tiêu gốc.
        var isBaseGoal = baseGoal == null;
        if (!isBaseGoal && dto.TargetValue <= baseGoal!.TargetValue)
            throw new ArgumentException($"Điểm nấc bổ sung ({dto.TargetValue:0.0}) phải lớn hơn mục tiêu gốc ({baseGoal.TargetValue:0.0}).");

        var goal = new Goal
        {
            UserId = userId,
            UserSubjectId = userSubject.UserSubjectId,
            Title = title,
            TargetValue = dto.TargetValue,
            EndDate = dto.EndDate,
            IsBaseGoal = isBaseGoal,
            Status = GradeConstants.GoalLifecycle.ToDo,
            DisplayOrder = isBaseGoal ? 0 : existingGoals.Max(g => g.DisplayOrder) + 1,
            IsCancelled = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var average = await goalStatusSync.GetSubjectAverageAsync(userId, userSubject.UserSubjectId, cancellationToken);
        GoalStatusEvaluator.ApplyTransition(goal, average, now, out _);

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

        var title = GradeGuard.RequireText(dto.Title, GradeConstants.GoalTitleMaxLength, "Tên mục tiêu");
        if (dto.EndDate != goal.EndDate) EnsureEndDateNotPast(dto.EndDate);
        var targetChanged = dto.TargetValue.HasValue && dto.TargetValue.Value != goal.TargetValue;

        // Mục tiêu gốc cố định: chỉ sửa tên và hạn.
        if (goal.IsBaseGoal && targetChanged)
            throw new InvalidOperationException("Mục tiêu gốc là cố định: Không được phép thay đổi điểm mục tiêu.");

        var now = DateTime.UtcNow;

        if (targetChanged)
        {
            var newTarget = dto.TargetValue!.Value;
            GradeGuard.EnsureTarget(newTarget);

            var baseGoal = await repository.Query<Goal>()
                .FirstOrDefaultAsync(g => g.UserId == userId && g.UserSubjectId == goal.UserSubjectId && !g.IsCancelled && g.IsBaseGoal, cancellationToken);
            if (baseGoal != null && newTarget <= baseGoal.TargetValue)
                throw new ArgumentException($"Điểm nấc bổ sung ({newTarget:0.0}) phải lớn hơn mục tiêu gốc ({baseGoal.TargetValue:0.0}).");

            var isDuplicate = await repository.AnyAsync<Goal>(
                g => g.UserId == userId && g.UserSubjectId == goal.UserSubjectId && g.GoalId != goal.GoalId && g.TargetValue == newTarget && !g.IsCancelled,
                cancellationToken);
            if (isDuplicate)
                throw new ArgumentException($"Môn học này đã có mục tiêu {newTarget:0.0}.");

            var average = await goalStatusSync.GetSubjectAverageAsync(userId, goal.UserSubjectId, cancellationToken);

            // Đánh giá theo mục tiêu cũ trước; nếu đang đạt thì lần đạt đó được lưu vào GoalHistory với mục tiêu cũ.
            var histories = new List<GoalHistory>();
            if (GoalStatusEvaluator.ApplyTransition(goal, average, now, out var dropped) && dropped is not null) histories.Add(dropped);
            var closed = GoalStatusEvaluator.CloseAchievement(goal, now);
            if (closed is not null) histories.Add(closed);

            goal.TargetValue = newTarget;
            GoalStatusEvaluator.ApplyTransition(goal, average, now, out _);

            foreach (var history in histories) await repository.AddAsync(history, cancellationToken);
        }

        goal.Title = title;
        goal.EndDate = dto.EndDate;
        goal.UpdatedAtUtc = now;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default)
    {
        var goal = await repository.Query<Goal>(false)
            .FirstOrDefaultAsync(g => g.GoalId == goalId, cancellationToken);

        if (goal is null) return;
        if (goal.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        // ── Quy tắc 4: Không được xóa mục tiêu gốc ───────────────────────────
        if (goal.IsBaseGoal)
        {
            throw new InvalidOperationException("Mục tiêu gốc là cố định: Không được phép xóa.");
        }

        // ── Quy tắc 1: Xử lý các ScheduleEvent đang liên kết ─────────────────
        // Hoạt động đã Done (IsCompleted == true): Giữ trên Lịch học cá nhân để bảo toàn lịch sử, gỡ liên kết GoalId = null.
        // Hoạt động chưa hoàn thành (IsCompleted == false): Soft-delete (IsDeleted = true).
        var linkedEvents = await repository.Query<ScheduleEvent>(false)
            .Where(e => e.GoalId == goal.GoalId && e.UserId == userId && !e.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var ev in linkedEvents)
        {
            if (ev.IsCompleted)
            {
                ev.GoalId = null;
                ev.UpdatedAtUtc = DateTime.UtcNow;
                repository.Update(ev);
            }
            else
            {
                ev.IsDeleted = true;
                ev.UpdatedAtUtc = DateTime.UtcNow;
                repository.Update(ev);
            }
        }

        goal.IsCancelled = true;
        goal.UpdatedAtUtc = DateTime.UtcNow;
        repository.Update(goal);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderGoalsAsync(long userId, List<ReorderGoalDto> items, CancellationToken cancellationToken = default)
    {
        if (items == null || !items.Any()) return;

        var goalIds = items.Select(i => i.GoalId).ToList();
        var goals = await repository.Query<Goal>(false)
            .Where(g => g.UserId == userId && goalIds.Contains(g.GoalId) && !g.IsCancelled && !g.IsBaseGoal)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var g = goals.FirstOrDefault(x => x.GoalId == item.GoalId);
            if (g != null)
            {
                g.DisplayOrder = item.NewDisplayOrder;
                g.UpdatedAtUtc = DateTime.UtcNow;
                repository.Update(g);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Q17: hạn hoàn thành (nếu có) không được ở quá khứ khi đặt/đổi hạn.</summary>
    private static void EnsureEndDateNotPast(DateOnly? endDate)
    {
        if (endDate.HasValue && endDate.Value < GradeClock.Today())
            throw new ArgumentException("Hạn hoàn thành không được ở quá khứ.");
    }

    // ── Goal Activities (Đồng bộ hai chiều với Lịch học cá nhân) ─────────
    // Q18 = B: không ghi ActivityEvent. Q19 = A: cho phép thời gian ở quá khứ; kết thúc phải sau bắt đầu.

    public async Task<ScheduleEventDto> AddGoalActivityAsync(long userId, CreateGoalActivityDto dto, CancellationToken cancellationToken = default)
    {
        var goal = await repository.Query<Goal>()
            .Include(g => g.UserSubject)
            .FirstOrDefaultAsync(g => g.GoalId == dto.GoalId && g.UserId == userId && !g.IsCancelled, cancellationToken);

        if (goal is null) throw new ArgumentException("Mục tiêu không tồn tại.");
        if (goal.UserSubject.IsArchived) throw new ArgumentException("Không thể thêm hoạt động cho môn đã lưu trữ.");

        var (title, description) = ValidateActivity(dto.Title, dto.Description, dto.StartAtLocal, dto.EndAtLocal);
        var now = DateTime.UtcNow;

        var activity = new ScheduleEvent
        {
            UserId = userId,
            UserSubjectId = goal.UserSubjectId,
            GoalId = goal.GoalId,
            Title = title,
            EventType = GradeConstants.GoalActivityEventType,
            StartAtLocal = dto.StartAtLocal,
            EndAtLocal = dto.EndAtLocal,
            TimeZoneId = GradeConstants.DefaultTimeZoneId,
            RepeatMode = GradeConstants.GoalActivityRepeatMode,
            Location = null,
            Description = description,
            IsCompleted = false,
            IsDeleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(activity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new ScheduleEventDto(
            activity.ScheduleEventId,
            activity.Title,
            activity.EventType,
            activity.StartAtLocal,
            activity.EndAtLocal,
            activity.TimeZoneId,
            activity.RepeatMode,
            activity.RepeatUntilDate,
            activity.Location,
            activity.Description,
            activity.GoalId,
            activity.IsCompleted
        );
    }

    public async Task UpdateGoalActivityAsync(long userId, UpdateGoalActivityDto dto, CancellationToken cancellationToken = default)
    {
        var activity = await GetGoalActivityAsync(userId, dto.ActivityId, cancellationToken)
            ?? throw new ArgumentException("Hoạt động không tồn tại.");

        var (title, description) = ValidateActivity(dto.Title, dto.Description, dto.StartAtLocal, dto.EndAtLocal);

        activity.Title = title;
        activity.StartAtLocal = dto.StartAtLocal;
        activity.EndAtLocal = dto.EndAtLocal;
        activity.Description = description;
        activity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGoalActivityAsync(long userId, long activityId, CancellationToken cancellationToken = default)
    {
        var activity = await GetGoalActivityAsync(userId, activityId, cancellationToken);
        if (activity is null) return;

        activity.IsDeleted = true;
        activity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ToggleGoalActivityCompletionAsync(long userId, long activityId, CancellationToken cancellationToken = default)
    {
        var activity = await GetGoalActivityAsync(userId, activityId, cancellationToken)
            ?? throw new ArgumentException("Hoạt động không tồn tại.");

        activity.IsCompleted = !activity.IsCompleted;
        activity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        return activity.IsCompleted;
    }

    /// <summary>Chỉ thao tác trên lịch thuộc một mục tiêu chưa hủy của người dùng (lịch thường do module Lịch quản lý).</summary>
    private Task<ScheduleEvent?> GetGoalActivityAsync(long userId, long activityId, CancellationToken cancellationToken) =>
        repository.Query<ScheduleEvent>(false)
            .FirstOrDefaultAsync(e => e.ScheduleEventId == activityId && e.UserId == userId && !e.IsDeleted &&
                                      e.GoalId != null && e.Goal!.UserId == userId && !e.Goal.IsCancelled,
                cancellationToken);

    private static (string Title, string? Description) ValidateActivity(string? title, string? description, DateTime startAtLocal, DateTime endAtLocal)
    {
        var cleanTitle = GradeGuard.RequireText(title, GradeConstants.ActivityTitleMaxLength, "Tên hoạt động");
        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (cleanDescription is { Length: > GradeConstants.ActivityDescriptionMaxLength })
            throw new ArgumentException($"Mô tả không được vượt quá {GradeConstants.ActivityDescriptionMaxLength} ký tự.");
        if (endAtLocal <= startAtLocal)
            throw new ArgumentException("Thời gian kết thúc phải sau thời gian bắt đầu.");
        return (cleanTitle, cleanDescription);
    }
}
