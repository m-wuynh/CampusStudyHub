using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class Deadline
{
    public long DeadlineId { get; set; }

    public long UserId { get; set; }

    public long? UserSubjectId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime DueAtUtc { get; set; }

    public byte Priority { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CompletedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();

    public virtual User User { get; set; } = null!;

    public virtual UserSubject? UserSubject { get; set; }
}
