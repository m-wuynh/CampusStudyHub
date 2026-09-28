using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class Document
{
    public long DocumentId { get; set; }

    public long OwnerUserId { get; set; }

    public long? UserSubjectId { get; set; }

    public long? StudyGroupId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string ResourceType { get; set; } = null!;

    public string? StorageKey { get; set; }

    public string? ExternalUrl { get; set; }

    public string? OriginalFileName { get; set; }

    public string? MimeType { get; set; }

    public long? FileSizeBytes { get; set; }

    public string Visibility { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<ContentReport> ContentReports { get; set; } = new List<ContentReport>();

    public virtual ICollection<DocumentBookmark> DocumentBookmarks { get; set; } = new List<DocumentBookmark>();

    public virtual User OwnerUser { get; set; } = null!;

    public virtual StudyGroup? StudyGroup { get; set; }

    public virtual UserSubject? UserSubject { get; set; }
}
