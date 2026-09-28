using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class FlashcardReview
{
    public long ReviewId { get; set; }

    public Guid RequestId { get; set; }

    public long UserId { get; set; }

    public long FlashcardId { get; set; }

    public byte Rating { get; set; }

    public int? ResponseMilliseconds { get; set; }

    public DateTime ReviewedAtUtc { get; set; }

    public virtual FlashcardProgress FlashcardProgress { get; set; } = null!;
}
