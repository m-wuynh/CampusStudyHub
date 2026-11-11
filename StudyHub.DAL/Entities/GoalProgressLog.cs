using System;

namespace StudyHub.DAL.Entities;

/// <summary>
/// Một mốc trong hành trình theo đuổi mục tiêu của một môn (UserSubject).
/// <list type="bullet">
/// <item>Mốc cấp môn (<see cref="GoalId"/> = null): ĐTB môn thay đổi (<c>ScoreChanged</c>).</item>
/// <item>Mốc cấp mục tiêu (<see cref="GoalId"/> có giá trị): tạo, đổi điểm mục tiêu, chuyển trạng thái, hủy.</item>
/// </list>
/// Chỉ thêm mới, không sửa; bị xóa cùng môn khi xóa môn/sổ điểm.
/// </summary>
public partial class GoalProgressLog
{
    public long GoalProgressLogId { get; set; }

    public long UserId { get; set; }

    public long UserSubjectId { get; set; }

    public long? GoalId { get; set; }

    /// <summary>GoalCreated | ScoreChanged | StatusChanged | TargetChanged | GoalCancelled.</summary>
    public string EventType { get; set; } = null!;

    /// <summary>ĐTB môn (thang 10) tại thời điểm ghi; null = chưa có điểm.</summary>
    public decimal? AverageScore { get; set; }

    /// <summary>ĐTB trước đó (ScoreChanged) hoặc điểm mục tiêu cũ (TargetChanged).</summary>
    public decimal? PreviousValue { get; set; }

    /// <summary>Điểm mục tiêu đang áp dụng tại thời điểm ghi (mốc cấp mục tiêu).</summary>
    public decimal? TargetValue { get; set; }

    public int? ProgressPercent { get; set; }

    public string? StatusFrom { get; set; }

    public string? StatusTo { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public virtual Goal? Goal { get; set; }
}
