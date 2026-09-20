using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class ActivityEvent
{
    public long ActivityEventId { get; set; }

    public Guid RequestId { get; set; }

    public long UserId { get; set; }

    public string EventType { get; set; } = null!;

    public DateTime OccurredAtUtc { get; set; }

    public DateOnly ActivityDate { get; set; }

    public string TimeZoneId { get; set; } = null!;

    public bool? IsStudyAction { get; set; }

    public virtual User User { get; set; } = null!;
}
