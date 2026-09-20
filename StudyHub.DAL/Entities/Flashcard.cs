using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class Flashcard
{
    public long FlashcardId { get; set; }

    public long DeckId { get; set; }

    public string FrontText { get; set; } = null!;

    public string BackText { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual FlashcardDeck Deck { get; set; } = null!;

    public virtual ICollection<FlashcardProgress> FlashcardProgresses { get; set; } = new List<FlashcardProgress>();
}
