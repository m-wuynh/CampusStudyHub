using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class VwDailyUsageUtc
{
    public DateOnly? ActivityDateUtc { get; set; }

    public long? EventCount { get; set; }

    public int? ActiveUsers { get; set; }

    public int? ActiveLearners { get; set; }
}
