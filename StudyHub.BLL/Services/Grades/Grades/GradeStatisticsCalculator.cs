using System;
using System.Collections.Generic;
using System.Linq;
using StudyHub.BLL.Constants;
using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Grades;

/// <summary>
/// Số liệu cho màn "Phân tích học tập". Chỉ đọc từ GradesPageDto đã có (không truy vấn DB, không ghi gì),
/// dùng lại GradeCalculator, GoalOverviewCalculator nên khớp số với bảng điểm và mục tiêu.
/// </summary>
public static class GradeStatisticsCalculator
{
    public static GradeStatisticsDto Build(GradesPageDto page, DateTime nowLocal)
    {
        var ladders = page.GoalLadders.Where(l => !l.IsDetached).ToList();
        var goals = ladders.SelectMany(l => l.Goals).ToList();
        var activities = goals.SelectMany(g => g.Activities ?? new List<GoalActivityDto>()).ToList();
        var completed = activities.Where(a => a.IsCompleted).ToList();
        var missed = activities.Count(a => !a.IsCompleted && a.EndAtLocal < nowLocal);

        // Phân bố điểm: mọi con điểm đã nhập trong sổ.
        var scores = page.SubjectDetails.SelectMany(s => s.Entries).Select(e => e.Score).ToList();
        var distribution = new List<ScoreBucketDto>
        {
            new("Dưới 5", scores.Count(x => x < 5m)),
            new("5 – 6.4", scores.Count(x => x >= 5m && x < 6.5m)),
            new("6.5 – 7.9", scores.Count(x => x >= 6.5m && x < 8m)),
            new("8 – 8.9", scores.Count(x => x >= 8m && x < 9m)),
            new("9 – 10", scores.Count(x => x >= 9m))
        };

        var weights = page.Columns.ToDictionary(c => c.GradeColumnId, c => c.Weight);
        var subjects = new List<SubjectStatDto>();
        var priorities = new List<StudyPriorityDto>();

        foreach (var subject in page.SubjectDetails)
        {
            var ladder = ladders.FirstOrDefault(l => l.UserSubjectId == subject.UserSubjectId);
            var focus = ladder is null ? null : GoalOverviewCalculator.FocusGoal(ladder);
            var shown = GradeCalculator.RoundForDisplay(subject.CurrentAverage10);

            // Thay đổi ĐTB: từ mốc đầu tiên trong hành trình đến ĐTB hiển thị hiện tại.
            var firstPoint = ladder?.Journey?.Points.FirstOrDefault();
            decimal? change = shown.HasValue && firstPoint != null ? shown.Value - firstPoint.Average : null;

            var subjectHours = Hours(ladder?.Goals
                .SelectMany(g => g.Activities ?? new List<GoalActivityDto>())
                .Where(a => a.IsCompleted));

            subjects.Add(new SubjectStatDto(
                subject.UserSubjectId, subject.SubjectName, subject.ColorHex,
                subject.CurrentAverage10, focus?.TargetValue, change, subjectHours));

            if (focus is null || !shown.HasValue || focus.GoalLifecycleStatus == GradeConstants.GoalLifecycle.Done) continue;

            var (sum, known, remaining) = GradeCalculator.SubjectWeights(
                subject.Entries.Select(e => (e.GradeColumnId, e.Score)), weights);

            priorities.Add(new StudyPriorityDto(
                subject.UserSubjectId,
                subject.SubjectName,
                shown.Value,
                focus.TargetValue,
                focus.TargetValue - shown.Value,
                focus.IsOverdue,
                focus.ProgressPercent,
                remaining > 0,
                remaining > 0 ? GradeCalculator.ComputeRequiredScore(sum, known, remaining, focus.TargetValue) : null));
        }

        return new GradeStatisticsDto(
            goals.Count(g => g.GoalLifecycleStatus == GradeConstants.GoalLifecycle.Done),
            goals.Count,
            Hours(completed),
            completed.Count,
            missed,
            scores.Count,
            distribution,
            subjects,
            priorities.OrderByDescending(p => p.IsOverdue).ThenByDescending(p => p.Gap).ToList());
    }

    private static decimal Hours(IEnumerable<GoalActivityDto>? activities) =>
        activities is null
            ? 0m
            : Math.Round((decimal)activities.Sum(a => (a.EndAtLocal - a.StartAtLocal).TotalHours), 1, MidpointRounding.AwayFromZero);
}
