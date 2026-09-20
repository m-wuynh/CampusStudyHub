using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class FlashcardProgress
{
    public long UserId { get; set; }

    public long FlashcardId { get; set; }

    public DateTime NextReviewAtUtc { get; set; }

    public DateTime? LastReviewedAtUtc { get; set; }

    public int RepetitionCount { get; set; }

    public int IntervalDays { get; set; }

    public decimal EaseFactor { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Flashcard Flashcard { get; set; } = null!;

    public virtual ICollection<FlashcardReview> FlashcardReviews { get; set; } = new List<FlashcardReview>();

    public virtual User User { get; set; } = null!;
}
