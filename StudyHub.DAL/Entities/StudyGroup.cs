using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class StudyGroup
{
    public long StudyGroupId { get; set; }

    public long OwnerUserId { get; set; }

    public int? SubjectId { get; set; }

    public string GroupName { get; set; } = null!;

    public string? Description { get; set; }

    public string Visibility { get; set; } = null!;

    public short MaxMembers { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<FlashcardDeck> FlashcardDecks { get; set; } = new List<FlashcardDeck>();

    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    public virtual ICollection<Note> Notes { get; set; } = new List<Note>();

    public virtual User OwnerUser { get; set; } = null!;

    public virtual Subject? Subject { get; set; }
}
