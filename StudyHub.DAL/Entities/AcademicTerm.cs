using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class AcademicTerm
{
    public long AcademicTermId { get; set; }

    public long UserId { get; set; }

    public string TermName { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsArchived { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<UserSubject> UserSubjects { get; set; } = new List<UserSubject>();
}
