using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class GradeEntry
{
    public long GradeEntryId { get; set; }

    public long UserSubjectId { get; set; }

    public long GradeColumnId { get; set; }

    /// <summary>Score on a scale of 0–10. Business rule: 0 &lt;= Score &lt;= 10.</summary>
    public decimal Score { get; set; }

    public DateOnly AssessedOn { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual GradeColumn GradeColumn { get; set; } = null!;

    public virtual UserSubject UserSubject { get; set; } = null!;
}
