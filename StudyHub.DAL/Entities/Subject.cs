using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class Subject
{
    public int SubjectId { get; set; }

    public string SubjectCode { get; set; } = null!;

    public string SubjectName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<StudyGroupSubject> StudyGroupSubjects { get; set; } = new List<StudyGroupSubject>();

    public virtual ICollection<UserSubject> UserSubjects { get; set; } = new List<UserSubject>();
}
