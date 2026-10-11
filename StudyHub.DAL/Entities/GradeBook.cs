using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class GradeBook
{
    public long GradeBookId { get; set; }

    public long UserId { get; set; }

    public int FromYear { get; set; }

    public int ToYear { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<GradeColumn> GradeColumns { get; set; } = new List<GradeColumn>();

    public virtual ICollection<UserSubject> UserSubjects { get; set; } = new List<UserSubject>();
}
