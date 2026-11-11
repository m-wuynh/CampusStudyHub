using System;
using System.Collections.Generic;
using System.Linq;
using StudyHub.BLL.Constants;
using StudyHub.BLL.DTOs;
using StudyHub.DAL.Entities;
using JourneyEvent = StudyHub.BLL.Constants.GradeConstants.GoalJourneyEvent;

namespace StudyHub.BLL.Services.Grades;

/// <summary>
/// Hành trình mục tiêu: tạo bản ghi <see cref="GoalProgressLog"/> (ghi) và dựng <see cref="GoalJourneyDto"/> (đọc).
/// Việc lưu do service gọi đảm nhận, trong cùng transaction với thao tác gốc.
/// </summary>
public static class GoalJourney
{
    /// <summary>Mốc cấp môn: ĐTB hiển thị của môn thay đổi.</summary>
    public static GoalProgressLog ScoreChanged(long userId, long userSubjectId, decimal? previousAverage, decimal? average, DateTime nowUtc) => new()
    {
        UserId = userId,
        UserSubjectId = userSubjectId,
        GoalId = null,
        EventType = JourneyEvent.ScoreChanged,
        PreviousValue = previousAverage,
        AverageScore = average,
        OccurredAtUtc = nowUtc
    };

    /// <summary>
    /// Mốc cấp mục tiêu (tạo, đổi trạng thái, đổi điểm mục tiêu, hủy). Gắn qua navigation <c>Goal</c>
    /// để dùng được cả với mục tiêu vừa tạo (chưa có GoalId).
    /// </summary>
    public static GoalProgressLog ForGoal(Goal goal, string eventType, decimal? average, string? statusFrom, DateTime nowUtc, decimal? previousValue = null) => new()
    {
        UserId = goal.UserId,
        UserSubjectId = goal.UserSubjectId,
        Goal = goal,
        EventType = eventType,
        AverageScore = average,
        PreviousValue = previousValue,
        TargetValue = goal.TargetValue,
        ProgressPercent = GoalStatusEvaluator.ProgressPercent(average, goal.TargetValue),
        StatusFrom = statusFrom,
        StatusTo = goal.Status,
        OccurredAtUtc = nowUtc
    };

    /// <summary>Thông tin mục tiêu cần để mô tả mốc (kể cả mục tiêu đã hủy).</summary>
    public sealed record GoalInfo(long GoalId, string Title, decimal TargetValue, bool IsBaseGoal);

    /// <summary>Dựng hành trình của một môn từ các mốc đã lưu (sắp xếp tăng dần theo thời gian).</summary>
    public static GoalJourneyDto Build(IEnumerable<GoalProgressLog> subjectLogs, IReadOnlyDictionary<long, GoalInfo> goals)
    {
        var ordered = subjectLogs
            .OrderBy(l => l.OccurredAtUtc)
            .ThenBy(l => l.GoalProgressLogId)
            .ToList();

        var events = ordered
            .Select(l =>
            {
                GoalInfo? info = l.GoalId.HasValue && goals.TryGetValue(l.GoalId.Value, out var g) ? g : null;
                return new GoalJourneyEventDto(
                    l.GoalProgressLogId,
                    l.EventType,
                    l.EventType == JourneyEvent.ScoreChanged ? GoalJourneyEventDto.CategoryScore : GoalJourneyEventDto.CategoryGoal,
                    l.GoalId,
                    info?.Title,
                    info?.IsBaseGoal ?? false,
                    GradeCalculator.RoundForDisplay(l.AverageScore),
                    l.EventType == JourneyEvent.ScoreChanged ? GradeCalculator.RoundForDisplay(l.PreviousValue) : l.PreviousValue,
                    l.TargetValue,
                    l.ProgressPercent,
                    l.StatusFrom,
                    l.StatusTo,
                    GradeClock.ToLocal(l.OccurredAtUtc));
            })
            .ToList();

        // Điểm của biểu đồ: mỗi thời điểm một điểm (mốc ĐTB và mốc trạng thái cùng lúc gộp làm một).
        var points = ordered
            .Where(l => l.AverageScore.HasValue)
            .GroupBy(l => l.OccurredAtUtc)
            .Select(g => new GoalJourneyPointDto(
                GradeClock.ToLocal(g.Key),
                GradeCalculator.RoundForDisplay(g.Last().AverageScore)!.Value))
            .ToList();

        // Mới nhất ở trên khi hiển thị danh sách.
        events.Reverse();
        return new GoalJourneyDto(events, points);
    }
}
