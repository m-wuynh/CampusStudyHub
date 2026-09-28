using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class VwStudyStreak
{
    public long UserId { get; set; }

    public DateOnly? LastStudyDate { get; set; }

    public int? LongestStreakDays { get; set; }

    public int? CurrentStreakDays { get; set; }
}
