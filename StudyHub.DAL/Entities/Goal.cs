using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class Goal
{
    public long GoalId { get; set; }

    public long UserId { get; set; }

    /// <summary>Always required – Goal must be linked to a UserSubject (Goal Ladder per subject).</summary>
    public long UserSubjectId { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>Target grade on a scale of 0–10. Business rule: 0 &lt; TargetValue &lt;= 10.</summary>
    public decimal TargetValue { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsCancelled { get; set; }

    public bool IsBaseGoal { get; set; }

    public string Status { get; set; } = "ToDo";

    public int DisplayOrder { get; set; }

    public decimal? AchievedScore { get; set; }

    public DateTime? AchievedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual UserSubject UserSubject { get; set; } = null!;

    public virtual ICollection<GoalHistory> GoalHistories { get; set; } = new List<GoalHistory>();

    public virtual ICollection<ScheduleEvent> ScheduleEvents { get; set; } = new List<ScheduleEvent>();
}

