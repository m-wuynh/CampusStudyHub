using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class Goal
{
    public long GoalId { get; set; }

    public long UserId { get; set; }

    public long? UserSubjectId { get; set; }

    public string Title { get; set; } = null!;

    public string UnitCode { get; set; } = null!;

    public decimal TargetValue { get; set; }

    public decimal CurrentValue { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsCancelled { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual UserSubject? UserSubject { get; set; }
}
