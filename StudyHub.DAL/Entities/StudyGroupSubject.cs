namespace StudyHub.DAL.Entities;

public sealed class StudyGroupSubject
{
    public long StudyGroupSubjectId { get; set; }

    public long StudyGroupId { get; set; }

    public int? SubjectId { get; set; }

    public string? CustomSubjectName { get; set; }

    public long AddedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public StudyGroup StudyGroup { get; set; } = null!;

    public Subject? Subject { get; set; }

    public User AddedByUser { get; set; } = null!;
}
