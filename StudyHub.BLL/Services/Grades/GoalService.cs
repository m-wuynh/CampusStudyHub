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
    Task DeleteGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default);
    Task SetGoalStatusAsync(long userId, SetGoalStatusDto dto, CancellationToken cancellationToken = default);
    Task DesignateBaseGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default);
    Task ReorderGoalsAsync(long userId, List<ReorderGoalDto> items, CancellationToken cancellationToken = default);

    // Goal Activities (2-way sync with ScheduleEvents)
    Task<ScheduleEventDto> AddGoalActivityAsync(long userId, CreateGoalActivityDto dto, CancellationToken cancellationToken = default);
    Task UpdateGoalActivityAsync(long userId, UpdateGoalActivityDto dto, CancellationToken cancellationToken = default);
    Task DeleteGoalActivityAsync(long userId, long activityId, CancellationToken cancellationToken = default);
    Task<bool> ToggleGoalActivityCompletionAsync(long userId, long activityId, CancellationToken cancellationToken = default);
}

public sealed class GoalService(IRepository repository) : IGoalService
{
    public async Task<List<GoalLadderDto>> GetGoalLaddersAsync(long userId, long termId, CancellationToken cancellationToken = default)
    {
        var userSubjects = await repository.Query<UserSubject>()
            .Include(us => us.Subject)
            .Include(us => us.GradeBook)
            .Where(us => us.UserId == userId && us.AcademicTermId == termId && !us.IsArchived)
            .ToListAsync(cancellationToken);

        if (!userSubjects.Any()) return new List<GoalLadderDto>();

        var subjectIds = userSubjects.Select(us => us.UserSubjectId).ToList();

        var goals = await repository.Query<Goal>()
            .Include(g => g.ScheduleEvents.Where(e => !e.IsDeleted))
            .Include(g => g.GoalHistories)
            .Where(g => g.UserId == userId && subjectIds.Contains(g.UserSubjectId))
            .ToListAsync(cancellationToken);

        var summaries = await repository.Query<VwSubjectGradeSummary>()
            .Where(v => v.UserId == userId && v.AcademicTermId == termId)
            .ToListAsync(cancellationToken);

        var ladders = new List<GoalLadderDto>();

        var subjectGroups = userSubjects
            .GroupBy(us => (us.CustomSubjectName ?? us.Subject?.SubjectName ?? "Môn học").Trim().ToLowerInvariant())
            .ToList();

        foreach (var group in subjectGroups)
        {
            var groupSubjectIds = group.Select(us => us.UserSubjectId).ToHashSet();
            var subjectGoals = goals.Where(g => groupSubjectIds.Contains(g.UserSubjectId) && !g.IsCancelled).ToList();
            if (!subjectGoals.Any()) continue;

            var canonicalSubject = group.FirstOrDefault(s => summaries.Any(sum => sum.UserSubjectId == s.UserSubjectId && sum.CurrentAverage10.HasValue)) ?? group.First();
            var avg = summaries.FirstOrDefault(s => groupSubjectIds.Contains(s.UserSubjectId) && s.CurrentAverage10.HasValue)?.CurrentAverage10;
            var subjectName = canonicalSubject.CustomSubjectName ?? canonicalSubject.Subject?.SubjectName ?? "Môn học";

            var baseGoal = subjectGoals.FirstOrDefault(g => g.IsBaseGoal);
            var additionalGoals = subjectGoals.Where(g => !g.IsBaseGoal).OrderBy(g => g.DisplayOrder).ThenBy(g => g.TargetValue).ToList();

            var goalDtos = new List<GoalDto>();
            foreach (var g in subjectGoals.OrderByDescending(g => g.IsBaseGoal).ThenBy(g => g.DisplayOrder).ThenBy(g => g.TargetValue))
            {
                var activities = g.ScheduleEvents.Select(e => new GoalActivityDto(
                    e.ScheduleEventId,
                    g.GoalId,
                    e.Title,
                    e.StartAtLocal,
                    e.EndAtLocal,
                    e.Description,
                    e.IsCompleted
                )).OrderBy(a => a.StartAtLocal).ToList();

                var histories = g.GoalHistories.Select(h => new GoalHistoryDto(
                    h.GoalHistoryId,
                    h.GoalId,
                    h.TargetValue,
                    h.AchievedScore,
                    h.AchievedAtUtc,
                    h.CreatedAtUtc
                )).OrderByDescending(h => h.AchievedAtUtc).ToList();

                string lifecycleStatus;
                if (avg.HasValue)
                {
                    if (avg.Value >= g.TargetValue) lifecycleStatus = "Done";
                    else lifecycleStatus = "InProgress";
                }
                else
                {
                    if (g.AchievedScore.HasValue || g.Status == "Done") lifecycleStatus = "Done";
                    else lifecycleStatus = "ToDo";
                }

                GoalStatus legacyStatus;
                if (lifecycleStatus == "Done") legacyStatus = GoalStatus.Achieved;
                else if (lifecycleStatus == "InProgress") legacyStatus = GoalStatus.InProgress;
                else legacyStatus = GoalStatus.NoData;

                goalDtos.Add(new GoalDto(
                    g.GoalId,
                    g.Title,
                    g.TargetValue,
                    avg,
                    g.EndDate,
                    g.UserSubjectId,
                    legacyStatus,
                    g.IsBaseGoal,
                    lifecycleStatus,
                    g.DisplayOrder,
                    g.AchievedScore,
                    g.AchievedAtUtc,
                    activities,
                    histories
                ));
            }

            var baseGoalDto = goalDtos.FirstOrDefault(g => g.IsBaseGoal);
            var addGoalDtos = goalDtos.Where(g => !g.IsBaseGoal).ToList();

            var gb = canonicalSubject.GradeBook ?? group.FirstOrDefault(s => s.GradeBook != null)?.GradeBook;
            string? academicYearName = null;
            if (gb != null)
            {
                academicYearName = (gb.FromYear > 0 && gb.ToYear > 0)
                    ? $"Năm học {gb.FromYear}–{gb.ToYear}"
                    : (!string.IsNullOrWhiteSpace(gb.Name) ? gb.Name : null);
            }

            ladders.Add(new GoalLadderDto(
                canonicalSubject.UserSubjectId,
                subjectName,
                avg,
                goalDtos,
                baseGoalDto != null,
                baseGoalDto,
                addGoalDtos,
                academicYearName
            ));
        }

        return ladders;
    }

    public async Task CreateGoalAsync(long userId, CreateGoalDto dto, CancellationToken cancellationToken = default)
    {
        // ── 1. Validate UserSubject ───────────────────────────────────
        var userSubject = await repository.Query<UserSubject>()
            .Include(us => us.Subject)
            .Include(us => us.GradeEntries)
            .FirstOrDefaultAsync(us => us.UserSubjectId == dto.UserSubjectId && us.UserId == userId, cancellationToken);

        if (userSubject is null) throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");
        if (userSubject.IsArchived) throw new ArgumentException("Không thể đặt mục tiêu cho môn đã lưu trữ.");

        // ── 2. Validate TargetValue ───────────────────────────────────
        if (dto.TargetValue <= 0 || dto.TargetValue > 10)
            throw new ArgumentException("Điểm mục tiêu phải nằm trong khoảng (0, 10].");

        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Tên mục tiêu không được để trống.");

        // ── 3. Find canonical subject and all sibling UserSubjects in the same GradeBook ──
        var subjectName = (userSubject.CustomSubjectName ?? userSubject.Subject?.SubjectName ?? "Môn học").Trim();
        var gradeBookId = userSubject.GradeBookId;

        var relatedSubjectsQuery = repository.Query<UserSubject>()
            .Include(us => us.Subject)
            .Include(us => us.GradeEntries)
            .Where(us => us.UserId == userId && !us.IsArchived);

        if (gradeBookId.HasValue)
        {
            relatedSubjectsQuery = relatedSubjectsQuery.Where(us => us.GradeBookId == gradeBookId.Value);
        }
        else
        {
            relatedSubjectsQuery = relatedSubjectsQuery.Where(us => us.GradeBookId == null);
        }

        var relatedSubjects = await relatedSubjectsQuery.ToListAsync(cancellationToken);
        var sameSubjectList = relatedSubjects
            .Where(us => string.Equals((us.CustomSubjectName ?? us.Subject?.SubjectName ?? "").Trim(), subjectName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var sameSubjectIds = sameSubjectList.Select(us => us.UserSubjectId).ToList();
        if (!sameSubjectIds.Contains(userSubject.UserSubjectId))
        {
            sameSubjectIds.Add(userSubject.UserSubjectId);
        }

        // ── 4. Check existing active goals for this subject in this GradeBook ─────────
        var existingGoals = await repository.Query<Goal>(false)
            .Where(g => g.UserId == userId && sameSubjectIds.Contains(g.UserSubjectId) && !g.IsCancelled)
            .OrderBy(g => g.CreatedAtUtc)
            .ThenBy(g => g.TargetValue)
            .ToListAsync(cancellationToken);

        // Check uniqueness of TargetValue within this subject in this GradeBook
        if (existingGoals.Any(g => g.TargetValue == dto.TargetValue))
            throw new ArgumentException($"Môn {subjectName} đã có mục tiêu {dto.TargetValue:0.0} đang hoạt động.");

        // Safe initialization of base goal for legacy data:
        // If existing goals exist but none has IsBaseGoal = true, the earliest becomes the base goal
        var existingBaseGoal = existingGoals.FirstOrDefault(g => g.IsBaseGoal);
        if (existingBaseGoal == null && existingGoals.Any())
        {
            existingBaseGoal = existingGoals.First();
            existingBaseGoal.IsBaseGoal = true;
            existingBaseGoal.UpdatedAtUtc = DateTime.UtcNow;
            repository.Update(existingBaseGoal);
            await repository.SaveChangesAsync(cancellationToken);
        }

        bool isBaseGoal;
        if (existingBaseGoal != null)
        {
            // Môn học đã có mục tiêu gốc => Mọi mục tiêu mới luôn là nấc bổ sung (phải > mục tiêu gốc và <= 10)
            if (dto.TargetValue <= existingBaseGoal.TargetValue)
            {
                throw new ArgumentException($"Điểm nấc bổ sung ({dto.TargetValue:0.0}) phải lớn hơn mục tiêu gốc ({existingBaseGoal.TargetValue:0.0}).");
            }
            isBaseGoal = false;
        }
        else
        {
            // Mục tiêu đầu tiên của môn học trong bảng điểm này tự động là Mục tiêu gốc
            isBaseGoal = true;
        }

        // All goals for the same subject in this GradeBook attach to the canonical UserSubjectId
        var targetUserSubjectId = existingBaseGoal != null ? existingBaseGoal.UserSubjectId : userSubject.UserSubjectId;
        var targetSubjectEntity = sameSubjectList.FirstOrDefault(us => us.UserSubjectId == targetUserSubjectId) ?? userSubject;

        var maxOrder = existingGoals.Any() ? existingGoals.Max(g => g.DisplayOrder) : 0;

        // Tính điểm trung bình môn thực tế theo bảng điểm của năm học này
        decimal? curAvg = null;
        if (targetSubjectEntity.GradeBookId.HasValue)
        {
            var columns = await repository.Query<GradeColumn>()
                .Where(c => c.GradeBookId == targetSubjectEntity.GradeBookId.Value)
                .ToListAsync(cancellationToken);
            curAvg = GradeCalculator.CalculateSubjectAverage(targetSubjectEntity.GradeEntries, columns);
        }

        string initialStatus = "ToDo";
        decimal? initialAchievedScore = null;
        DateTime? initialAchievedAt = null;

        if (curAvg.HasValue)
        {
            if (curAvg.Value >= dto.TargetValue)
            {
                initialStatus = "Done";
                initialAchievedScore = curAvg.Value;
                initialAchievedAt = DateTime.UtcNow;
            }
            else
            {
                initialStatus = "InProgress";
            }
        }

        var goal = new Goal
        {
            UserId = userId,
            UserSubjectId = targetUserSubjectId,
            Title = dto.Title.Trim(),
            TargetValue = dto.TargetValue,
            EndDate = dto.EndDate,
            IsBaseGoal = isBaseGoal,
            Status = initialStatus,
            AchievedScore = initialAchievedScore,
            AchievedAtUtc = initialAchievedAt,
            DisplayOrder = isBaseGoal ? 0 : maxOrder + 1,
            IsCancelled = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
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

        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Tên mục tiêu không được để trống.");

        // ── Quy tắc: Mục tiêu gốc là cố định, KHÔNG được sửa điểm ──────────
        if (goal.IsBaseGoal)
        {
            if (dto.TargetValue.HasValue && dto.TargetValue.Value != goal.TargetValue)
            {
                throw new InvalidOperationException("Mục tiêu gốc là cố định: Không được phép thay đổi điểm mục tiêu.");
            }
            goal.Title = dto.Title.Trim();
            goal.EndDate = dto.EndDate;
            goal.UpdatedAtUtc = DateTime.UtcNow;
            repository.Update(goal);
            await repository.SaveChangesAsync(cancellationToken);
            return;
        }

        // ── Nấc bổ sung: cho phép sửa điểm mục tiêu ────────────────────────
        if (dto.TargetValue.HasValue && dto.TargetValue.Value != goal.TargetValue)
        {
            var newTarget = dto.TargetValue.Value;
            if (newTarget <= 0 || newTarget > 10)
                throw new ArgumentException("Điểm mục tiêu phải nằm trong khoảng (0, 10].");

            // Check against base goal across all sibling subjects in the same GradeBook
            var goalSubject = await repository.Query<UserSubject>()
                .Include(us => us.Subject)
                .FirstOrDefaultAsync(us => us.UserSubjectId == goal.UserSubjectId, cancellationToken);

            var sName = (goalSubject?.CustomSubjectName ?? goalSubject?.Subject?.SubjectName ?? string.Empty).Trim();
            var gbId = goalSubject?.GradeBookId;

            var siblingSubjects = await repository.Query<UserSubject>()
                .Include(us => us.Subject)
                .Where(us => us.UserId == userId && us.GradeBookId == gbId && !us.IsArchived)
                .ToListAsync(cancellationToken);

            var siblingIds = siblingSubjects
                .Where(us => string.Equals((us.CustomSubjectName ?? us.Subject?.SubjectName ?? string.Empty).Trim(), sName, StringComparison.OrdinalIgnoreCase))
                .Select(us => us.UserSubjectId)
                .ToList();

            if (!siblingIds.Contains(goal.UserSubjectId)) siblingIds.Add(goal.UserSubjectId);

            var baseGoal = await repository.Query<Goal>()
                .FirstOrDefaultAsync(g => g.UserId == userId && siblingIds.Contains(g.UserSubjectId) && !g.IsCancelled && g.IsBaseGoal, cancellationToken);

            if (baseGoal != null && newTarget <= baseGoal.TargetValue)
            {
                throw new ArgumentException($"Điểm nấc bổ sung ({newTarget:0.0}) phải lớn hơn mục tiêu gốc ({baseGoal.TargetValue:0.0}).");
            }

            // Check uniqueness
            var isDuplicate = await repository.AnyAsync<Goal>(
                g => g.UserId == userId && siblingIds.Contains(g.UserSubjectId) && g.GoalId != goal.GoalId && g.TargetValue == newTarget && !g.IsCancelled,
                cancellationToken);
            if (isDuplicate)
                throw new ArgumentException($"Môn học này đã có mục tiêu {newTarget:0.0}.");

            // Check current subject grade average
            decimal? curAvg = null;
            var subject = await repository.Query<UserSubject>()
                .Include(us => us.GradeEntries)
                .FirstOrDefaultAsync(us => us.UserSubjectId == goal.UserSubjectId && us.UserId == userId, cancellationToken);
            if (subject != null && subject.GradeBookId.HasValue)
            {
                var columns = await repository.Query<GradeColumn>()
                    .Where(c => c.GradeBookId == subject.GradeBookId.Value)
                    .ToListAsync(cancellationToken);
                curAvg = GradeCalculator.CalculateSubjectAverage(subject.GradeEntries, columns);
            }

            // ── Quy tắc 2: Nếu nấc đã Done, lưu lịch sử thành tích cũ trước khi cập nhật mục tiêu mới ──
            if (goal.Status == "Done")
            {
                var history = new GoalHistory
                {
                    GoalId = goal.GoalId,
                    TargetValue = goal.TargetValue,
                    AchievedScore = goal.AchievedScore ?? goal.TargetValue,
                    AchievedAtUtc = goal.AchievedAtUtc ?? DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow
                };
                await repository.AddAsync(history, cancellationToken);

                goal.TargetValue = newTarget;
                if (curAvg.HasValue)
                {
                    if (curAvg.Value >= newTarget)
                    {
                        goal.Status = "Done";
                        goal.AchievedScore = curAvg.Value;
                        goal.AchievedAtUtc = DateTime.UtcNow;
                    }
                    else
                    {
                        goal.Status = "InProgress";
                        goal.AchievedScore = null;
                        goal.AchievedAtUtc = null;
                    }
                }
                else
                {
                    goal.Status = "ToDo";
                    goal.AchievedScore = null;
                    goal.AchievedAtUtc = null;
                }
            }
            else
            {
                goal.TargetValue = newTarget;
                if (curAvg.HasValue)
                {
                    if (curAvg.Value >= newTarget)
                    {
                        goal.Status = "Done";
                        goal.AchievedScore = curAvg.Value;
                        goal.AchievedAtUtc = DateTime.UtcNow;
                    }
                    else
                    {
                        goal.Status = "InProgress";
                    }
                }
                else
                {
                    goal.Status = "ToDo";
                }
            }
        }

        goal.Title = dto.Title.Trim();
        goal.EndDate = dto.EndDate;
        goal.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(goal);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default)
    {
        await DeleteGoalAsync(userId, goalId, cancellationToken);
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

    public Task SetGoalStatusAsync(long userId, SetGoalStatusDto dto, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Trạng thái mục tiêu được hệ thống xác định tự động theo điểm số thực tế, không thể chỉnh sửa thủ công.");
    }

    public async Task DesignateBaseGoalAsync(long userId, long goalId, CancellationToken cancellationToken = default)
    {
        // ── Quy tắc 5: Chỉ định nấc có sẵn làm Mục tiêu gốc ──────────────────
        var goal = await repository.Query<Goal>(false)
            .FirstOrDefaultAsync(g => g.GoalId == goalId && g.UserId == userId && !g.IsCancelled, cancellationToken);

        if (goal is null) throw new ArgumentException("Mục tiêu không tồn tại.");

        var existingBase = await repository.Query<Goal>()
            .FirstOrDefaultAsync(g => g.UserId == userId && g.UserSubjectId == goal.UserSubjectId && !g.IsCancelled && g.IsBaseGoal, cancellationToken);

        if (existingBase != null)
        {
            throw new InvalidOperationException("Môn học này đã có mục tiêu gốc.");
        }

        // Kiểm tra xem có nấc nào khác có điểm thấp hơn nấc này không (để đảm bảo nấc bổ sung luôn > mục tiêu gốc)
        var hasLowerRung = await repository.Query<Goal>()
            .AnyAsync(g => g.UserId == userId && g.UserSubjectId == goal.UserSubjectId && !g.IsCancelled && g.GoalId != goal.GoalId && g.TargetValue < goal.TargetValue, cancellationToken);

        if (hasLowerRung)
        {
            throw new InvalidOperationException($"Không thể chọn nấc {goal.TargetValue:0.0} làm mục tiêu gốc vì môn học còn có nấc điểm thấp hơn.");
        }

        goal.IsBaseGoal = true;
        goal.DisplayOrder = 0;
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

    // ── Goal Activities (Đồng bộ hai chiều với Lịch học cá nhân) ─────────

    public async Task<ScheduleEventDto> AddGoalActivityAsync(long userId, CreateGoalActivityDto dto, CancellationToken cancellationToken = default)
    {
        var goal = await repository.Query<Goal>()
            .FirstOrDefaultAsync(g => g.GoalId == dto.GoalId && g.UserId == userId && !g.IsCancelled, cancellationToken);

        if (goal is null) throw new ArgumentException("Mục tiêu không tồn tại.");
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ArgumentException("Tên hoạt động không được để trống.");

        var startLocal = dto.StartAtLocal;
        var endLocal = dto.EndAtLocal;
        if (endLocal <= startLocal)
        {
            endLocal = startLocal.AddHours(1);
        }

        var activity = new ScheduleEvent
        {
            UserId = userId,
            UserSubjectId = goal.UserSubjectId,
            GoalId = goal.GoalId,
            Title = dto.Title.Trim(),
            EventType = "SelfStudy",
            StartAtLocal = startLocal,
            EndAtLocal = endLocal,
            TimeZoneId = "SE Asia Standard Time",
            RepeatMode = "None",
            Location = null,
            Description = dto.Description?.Trim(),
            IsCompleted = false,
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
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
        var activity = await repository.Query<ScheduleEvent>(false)
            .FirstOrDefaultAsync(e => e.ScheduleEventId == dto.ActivityId && e.UserId == userId && !e.IsDeleted, cancellationToken);

        if (activity is null) throw new ArgumentException("Hoạt động không tồn tại.");
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ArgumentException("Tên hoạt động không được để trống.");

        var startLocal = dto.StartAtLocal;
        var endLocal = dto.EndAtLocal;
        if (endLocal <= startLocal) endLocal = startLocal.AddHours(1);

        activity.Title = dto.Title.Trim();
        activity.StartAtLocal = startLocal;
        activity.EndAtLocal = endLocal;
        activity.Description = dto.Description?.Trim();
        activity.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(activity);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGoalActivityAsync(long userId, long activityId, CancellationToken cancellationToken = default)
    {
        var activity = await repository.Query<ScheduleEvent>(false)
            .FirstOrDefaultAsync(e => e.ScheduleEventId == activityId && e.UserId == userId && !e.IsDeleted, cancellationToken);

        if (activity is null) return;

        activity.IsDeleted = true;
        activity.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(activity);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ToggleGoalActivityCompletionAsync(long userId, long activityId, CancellationToken cancellationToken = default)
    {
        var activity = await repository.Query<ScheduleEvent>(false)
            .FirstOrDefaultAsync(e => e.ScheduleEventId == activityId && e.UserId == userId && !e.IsDeleted, cancellationToken);

        if (activity is null) throw new ArgumentException("Hoạt động không tồn tại.");

        activity.IsCompleted = !activity.IsCompleted;
        activity.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(activity);
        await repository.SaveChangesAsync(cancellationToken);

        return activity.IsCompleted;
    }
}
