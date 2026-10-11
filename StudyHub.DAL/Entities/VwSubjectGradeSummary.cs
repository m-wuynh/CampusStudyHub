using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class VwSubjectGradeSummary
{
    public long UserSubjectId { get; set; }

    public long UserId { get; set; }

    public long AcademicTermId { get; set; }

    public int? SubjectId { get; set; }

    public string SubjectName { get; set; } = null!;

    public decimal CreditWeight { get; set; }

    public int? EnteredAssessmentCount { get; set; }

    /// <summary>
    /// Weighted average on scale 0–10.
    /// Formula: SUM(Score * Weight) / SUM(Weight) across active (non-deleted) grade entries
    /// in non-archived columns. Null if no valid entries exist.
    /// </summary>
    public decimal? CurrentAverage10 { get; set; }
}
