using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class UserSubject
{
    public long UserSubjectId { get; set; }

    public long UserId { get; set; }

    public long AcademicTermId { get; set; }

    public int SubjectId { get; set; }

    public string? TeacherName { get; set; }

    public string? ClassCode { get; set; }

    public decimal CreditWeight { get; set; }

    public decimal? TargetScore10 { get; set; }

    public string ColorHex { get; set; } = null!;

    public bool IsArchived { get; set; }

    public virtual AcademicTerm AcademicTerm { get; set; } = null!;

    public virtual ICollection<Deadline> Deadlines { get; set; } = new List<Deadline>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<FlashcardDeck> FlashcardDecks { get; set; } = new List<FlashcardDeck>();

    public virtual ICollection<Goal> Goals { get; set; } = new List<Goal>();

    public virtual ICollection<GradeEntry> GradeEntries { get; set; } = new List<GradeEntry>();

    public virtual ICollection<Note> Notes { get; set; } = new List<Note>();

    public virtual ICollection<ScheduleEvent> ScheduleEvents { get; set; } = new List<ScheduleEvent>();

    public virtual ICollection<StudySession> StudySessions { get; set; } = new List<StudySession>();

    public virtual Subject Subject { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
