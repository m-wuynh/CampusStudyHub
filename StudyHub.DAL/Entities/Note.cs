using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class Note
{
    public long NoteId { get; set; }

    public long OwnerUserId { get; set; }

    public long? UserSubjectId { get; set; }

    public long? StudyGroupId { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string BodyFormat { get; set; } = null!;

    public string Visibility { get; set; } = null!;

    public bool IsPinned { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<ContentReport> ContentReports { get; set; } = new List<ContentReport>();

    public virtual User OwnerUser { get; set; } = null!;

    public virtual StudyGroup? StudyGroup { get; set; }

    public virtual UserSubject? UserSubject { get; set; }
}
