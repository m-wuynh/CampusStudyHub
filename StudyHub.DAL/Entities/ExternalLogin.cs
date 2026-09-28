using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class ExternalLogin
{
    public string Provider { get; set; } = null!;

    public string ProviderSubject { get; set; } = null!;

    public long UserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
}
