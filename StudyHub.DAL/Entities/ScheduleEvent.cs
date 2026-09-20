using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class ScheduleEvent
{
    public long ScheduleEventId { get; set; }

    public long UserId { get; set; }

    public long? UserSubjectId { get; set; }

    public string Title { get; set; } = null!;

    public string EventType { get; set; } = null!;

    public DateTime StartAtLocal { get; set; }

    public DateTime EndAtLocal { get; set; }

    public string TimeZoneId { get; set; } = null!;

    public string RepeatMode { get; set; } = null!;

    public DateOnly? RepeatUntilDate { get; set; }

    public string? Location { get; set; }

    public string? Description { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();

    public virtual ICollection<ScheduleException> ScheduleExceptions { get; set; } = new List<ScheduleException>();

    public virtual User User { get; set; } = null!;

    public virtual UserSubject? UserSubject { get; set; }
}
