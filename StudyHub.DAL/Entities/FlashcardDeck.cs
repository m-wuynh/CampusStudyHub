using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class FlashcardDeck
{
    public long DeckId { get; set; }

    public long OwnerUserId { get; set; }

    public long? UserSubjectId { get; set; }

    public long? StudyGroupId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string Visibility { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual ICollection<ContentReport> ContentReports { get; set; } = new List<ContentReport>();

    public virtual ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();

    public virtual User OwnerUser { get; set; } = null!;

    public virtual StudyGroup? StudyGroup { get; set; }

    public virtual UserSubject? UserSubject { get; set; }
}
