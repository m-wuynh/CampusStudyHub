using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class VwSubjectGradeSummary
{
    public long UserSubjectId { get; set; }

    public long UserId { get; set; }

    public long AcademicTermId { get; set; }

    public int SubjectId { get; set; }

    public string SubjectName { get; set; } = null!;

    public decimal CreditWeight { get; set; }

    public decimal? TargetScore10 { get; set; }

    public int? EnteredAssessmentCount { get; set; }

    public decimal? CurrentAverage10 { get; set; }
}
