using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class ScheduleException
{
    public long ScheduleEventId { get; set; }

    public DateOnly OccurrenceDate { get; set; }

    public bool IsCancelled { get; set; }

    public DateTime? NewStartAtLocal { get; set; }

    public DateTime? NewEndAtLocal { get; set; }

    public string? NewLocation { get; set; }

    public virtual ScheduleEvent ScheduleEvent { get; set; } = null!;
}
