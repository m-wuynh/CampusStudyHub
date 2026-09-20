using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class GroupPost
{
    public long GroupPostId { get; set; }

    public long StudyGroupId { get; set; }

    public long AuthorUserId { get; set; }

    public string Body { get; set; } = null!;

    public bool IsPinned { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual ICollection<ContentReport> ContentReports { get; set; } = new List<ContentReport>();

    public virtual GroupMember GroupMember { get; set; } = null!;
}
