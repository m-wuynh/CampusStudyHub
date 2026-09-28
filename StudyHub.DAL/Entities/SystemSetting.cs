using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class SystemSetting
{
    public string SettingKey { get; set; } = null!;

    public string SettingValue { get; set; } = null!;

    public string? Description { get; set; }

    public long? UpdatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual User? UpdatedByUser { get; set; }
}
