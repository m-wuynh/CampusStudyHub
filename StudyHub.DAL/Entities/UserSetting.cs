using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class UserSetting
{
    public long UserId { get; set; }

    public string Theme { get; set; } = null!;

    public string LanguageCode { get; set; } = null!;

    public string TimeZoneId { get; set; } = null!;

    public bool EmailRemindersEnabled { get; set; }

    public bool InAppRemindersEnabled { get; set; }

    public short DefaultReminderMinutes { get; set; }

    public short DailyStudyGoalMinutes { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
}
