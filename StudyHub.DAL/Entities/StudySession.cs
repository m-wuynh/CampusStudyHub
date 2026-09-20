using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class StudySession
{
    public long StudySessionId { get; set; }

    public long UserId { get; set; }

    public long? UserSubjectId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public int FocusSeconds { get; set; }

    public string Status { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual UserSubject? UserSubject { get; set; }
}
