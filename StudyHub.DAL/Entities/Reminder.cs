using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class Reminder
{
    public long ReminderId { get; set; }

    public long UserId { get; set; }

    public long? DeadlineId { get; set; }

    public long? ScheduleEventId { get; set; }

    public DateOnly? OccurrenceDate { get; set; }

    public string Channel { get; set; } = null!;

    public DateTime RemindAtUtc { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? SentAtUtc { get; set; }

    public string? LastError { get; set; }

    public virtual Deadline? Deadline { get; set; }

    public virtual ScheduleEvent? ScheduleEvent { get; set; }

    public virtual User User { get; set; } = null!;
}
