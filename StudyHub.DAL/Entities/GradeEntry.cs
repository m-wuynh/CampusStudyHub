using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class GradeEntry
{
    public long GradeEntryId { get; set; }

    public long UserSubjectId { get; set; }

    public string Title { get; set; } = null!;

    public string AssessmentType { get; set; } = null!;

    public decimal Score { get; set; }

    public decimal MaxScore { get; set; }

    public decimal Weight { get; set; }

    public DateOnly AssessedOn { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual UserSubject UserSubject { get; set; } = null!;
}
