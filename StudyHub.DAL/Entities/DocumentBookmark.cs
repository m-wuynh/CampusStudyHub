using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class DocumentBookmark
{
    public long UserId { get; set; }

    public long DocumentId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Document Document { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
