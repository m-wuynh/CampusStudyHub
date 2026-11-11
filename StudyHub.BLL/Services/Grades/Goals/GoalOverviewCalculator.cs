using System;
using System.Collections.Generic;
using System.Linq;
using StudyHub.BLL.Constants;
using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Grades;

/// <summary>Tổng quan mục tiêu cho Dashboard (Q15 = A). Chỉ đọc, chỉ dùng ladder của môn thuộc sổ.</summary>
public static class GoalOverviewCalculator
{
    public const int MaxHighlights = 3;

    public static GoalOverviewDto? Build(IEnumerable<GoalLadderDto> goalLadders)
    {
        var ladders = goalLadders.Where(l => !l.IsDetached && l.BaseGoal != null && l.Goals.Count > 0).ToList();
        if (ladders.Count == 0) return null;

        var baseGoals = ladders.Select(l => l.BaseGoal!).ToList();

        // % tổng = trung bình % theo mục tiêu gốc (môn chưa có điểm tính 0%).
        var overallProgress = (int)Math.Floor(baseGoals.Average(g => (decimal)(g.ProgressPercent ?? 0)));
        var targetAverage = Math.Round(baseGoals.Average(g => g.TargetValue), GradeConstants.DisplayDecimals, MidpointRounding.AwayFromZero);

        // Mỗi môn: nấc chưa đạt thấp nhất (đã đạt hết → nấc cao nhất); lấy các môn có % thấp nhất.
        var highlights = ladders
            .Select(l =>
            {
                var goal = FocusGoal(l)!;
                return new GoalHighlightDto(l.SubjectName, goal.TargetValue, l.CurrentAverage10, goal.ProgressPercent ?? 0);
            })
            .OrderBy(h => h.ProgressPercent)
            .ThenBy(h => h.SubjectName)
            .Take(MaxHighlights)
            .ToList();

        return new GoalOverviewDto(overallProgress, targetAverage, highlights);
    }

    /// <summary>
    /// Nấc đang theo đuổi của một môn: nấc chưa đạt có mục tiêu thấp nhất; đạt hết → nấc cao nhất.
    /// Dùng chung cho Dashboard và cột "Tiến độ" của bảng điểm.
    /// </summary>
    public static GoalDto? FocusGoal(GoalLadderDto ladder) =>
        ladder.Goals.Where(g => g.GoalLifecycleStatus != GradeConstants.GoalLifecycle.Done).OrderBy(g => g.TargetValue).FirstOrDefault()
        ?? ladder.Goals.OrderByDescending(g => g.TargetValue).FirstOrDefault();
}
