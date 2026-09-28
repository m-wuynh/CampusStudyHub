using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class ContentReport
{
    public long ReportId { get; set; }

    public long ReporterUserId { get; set; }

    public long? NoteId { get; set; }

    public long? DocumentId { get; set; }

    public long? DeckId { get; set; }

    public long? GroupPostId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public long? ResolvedByUserId { get; set; }

    public string? ResolutionNote { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public virtual FlashcardDeck? Deck { get; set; }

    public virtual Document? Document { get; set; }

    public virtual GroupPost? GroupPost { get; set; }

    public virtual Note? Note { get; set; }

    public virtual User ReporterUser { get; set; } = null!;

    public virtual User? ResolvedByUser { get; set; }
}
