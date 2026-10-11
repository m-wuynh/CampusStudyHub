using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class GradeColumn
{
    public long GradeColumnId { get; set; }

    public long GradeBookId { get; set; }

    public string Name { get; set; } = null!;

    public decimal Weight { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual GradeBook GradeBook { get; set; } = null!;

    public virtual ICollection<GradeEntry> GradeEntries { get; set; } = new List<GradeEntry>();
}
