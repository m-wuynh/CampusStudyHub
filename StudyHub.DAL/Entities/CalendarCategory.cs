using System;

namespace StudyHub.DAL.Entities;

public partial class CalendarCategory
{
    public long CategoryId { get; set; }

    public long UserId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
}
