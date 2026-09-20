using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class GroupMember
{
    public long StudyGroupId { get; set; }

    public long UserId { get; set; }

    public string MemberRole { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime RequestedAtUtc { get; set; }

    public DateTime? JoinedAtUtc { get; set; }

    public virtual ICollection<GroupPost> GroupPosts { get; set; } = new List<GroupPost>();

    public virtual StudyGroup StudyGroup { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
