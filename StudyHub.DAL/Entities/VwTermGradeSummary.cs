using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class VwTermGradeSummary
{
    public long UserId { get; set; }

    public long AcademicTermId { get; set; }

    public int? SubjectsWithGrades { get; set; }

    public decimal? CurrentAverage10 { get; set; }
}
