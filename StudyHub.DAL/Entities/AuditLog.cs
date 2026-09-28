using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public long? ActorUserId { get; set; }

    public string ActionCode { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public string EntityKey { get; set; } = null!;

    public string? Summary { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual User? ActorUser { get; set; }
}
