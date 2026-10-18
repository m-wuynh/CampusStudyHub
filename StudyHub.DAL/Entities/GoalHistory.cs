using System;

namespace StudyHub.DAL.Entities;

public partial class GoalHistory
{
    public long GoalHistoryId { get; set; }

    public long GoalId { get; set; }

    public decimal TargetValue { get; set; }

    public decimal AchievedScore { get; set; }

    public DateTime AchievedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Goal Goal { get; set; } = null!;
}
