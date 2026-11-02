using System;
using StudyHub.BLL.DTOs;
using StudyHub.DAL.Entities;
using Lifecycle = StudyHub.BLL.Constants.GradeConstants.GoalLifecycle;

namespace StudyHub.BLL.Services.Grades;

/// <summary>
/// Mô hình trạng thái mục tiêu (Q05–Q08): trạng thái luôn suy ra từ ĐTB hiện tại của đúng môn,
/// so sánh bằng ĐTB làm tròn 1 chữ số (số đang hiển thị).
/// <list type="bullet">
/// <item>Chưa có điểm → ToDo; ĐTB ≥ mục tiêu → Done; còn lại → InProgress.</item>
/// <item><c>AchievedScore/AchievedAtUtc</c> = lần đạt gần nhất. Khi rời Done, lần đạt đó được ghi vào GoalHistory rồi xóa.</item>
/// </list>
/// </summary>
public static class GoalStatusEvaluator
{
    public static string Evaluate(decimal? average, decimal target)
    {
        var shown = GradeCalculator.RoundForDisplay(average);
        if (shown is null) return Lifecycle.ToDo;
        return shown.Value >= target ? Lifecycle.Done : Lifecycle.InProgress;
    }

    public static GoalStatus ToLegacyStatus(string lifecycleStatus) => lifecycleStatus switch
    {
        Lifecycle.Done => GoalStatus.Achieved,
        Lifecycle.InProgress => GoalStatus.InProgress,
        _ => GoalStatus.NoData
    };

    /// <summary>% tiến độ theo ĐTB hiển thị (Q08), dùng chung cho ladder, bảng điểm và Dashboard.</summary>
    public static int? ProgressPercent(decimal? average, decimal target) =>
        GradeCalculator.ComputeProgressPercent(GradeCalculator.RoundForDisplay(average), target);

    /// <summary>
    /// Cập nhật <c>Status</c>/<c>AchievedScore</c>/<c>AchievedAtUtc</c> theo ĐTB. Trả về true nếu goal thay đổi;
    /// <paramref name="history"/> khác null khi một lần đạt vừa kết thúc (cần thêm vào DB).
    /// </summary>
    public static bool ApplyTransition(Goal goal, decimal? average, DateTime nowUtc, out GoalHistory? history)
    {
        history = null;
        var next = Evaluate(average, goal.TargetValue);

        if (next == Lifecycle.Done)
        {
            if (goal.Status == Lifecycle.Done && goal.AchievedScore.HasValue) return false;
            goal.AchievedScore = average!.Value;
            goal.AchievedAtUtc = nowUtc;
        }
        else
        {
            history = CloseAchievement(goal, nowUtc);
            if (goal.Status == next && history is null) return false;
        }

        goal.Status = next;
        goal.UpdatedAtUtc = nowUtc;
        return true;
    }

    /// <summary>
    /// Kết thúc lần đạt hiện tại (nếu có): trả về bản ghi GoalHistory với mục tiêu đang áp dụng,
    /// xóa <c>AchievedScore/AchievedAtUtc</c> và đưa goal về InProgress chờ đánh giá lại.
    /// </summary>
    public static GoalHistory? CloseAchievement(Goal goal, DateTime nowUtc)
    {
        if (goal.Status != Lifecycle.Done && !goal.AchievedScore.HasValue) return null;

        var history = new GoalHistory
        {
            GoalId = goal.GoalId,
            TargetValue = goal.TargetValue,
            AchievedScore = goal.AchievedScore ?? goal.TargetValue,
            AchievedAtUtc = goal.AchievedAtUtc ?? nowUtc,
            CreatedAtUtc = nowUtc
        };
        goal.AchievedScore = null;
        goal.AchievedAtUtc = null;
        goal.Status = Lifecycle.InProgress;
        goal.UpdatedAtUtc = nowUtc;
        return history;
    }
}
