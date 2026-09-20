using System;
using System.Collections.Generic;

namespace StudyHub.DAL.Entities;

public partial class User
{
    public long UserId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string? Email { get; set; }

    public string? AvatarUrl { get; set; }

    public string EducationLevel { get; set; } = null!;

    public string? SchoolName { get; set; }

    public string? ClassName { get; set; }

    public string? StudentCode { get; set; }

    public string RoleCode { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<AcademicTerm> AcademicTerms { get; set; } = new List<AcademicTerm>();

    public virtual ICollection<ActivityEvent> ActivityEvents { get; set; } = new List<ActivityEvent>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<ContentReport> ContentReportReporterUsers { get; set; } = new List<ContentReport>();

    public virtual ICollection<ContentReport> ContentReportResolvedByUsers { get; set; } = new List<ContentReport>();

    public virtual ICollection<Deadline> Deadlines { get; set; } = new List<Deadline>();

    public virtual ICollection<DocumentBookmark> DocumentBookmarks { get; set; } = new List<DocumentBookmark>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();

    public virtual ICollection<FlashcardDeck> FlashcardDecks { get; set; } = new List<FlashcardDeck>();

    public virtual ICollection<FlashcardProgress> FlashcardProgresses { get; set; } = new List<FlashcardProgress>();

    public virtual ICollection<Goal> Goals { get; set; } = new List<Goal>();

    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    public virtual ICollection<Note> Notes { get; set; } = new List<Note>();

    public virtual ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();

    public virtual ICollection<ScheduleEvent> ScheduleEvents { get; set; } = new List<ScheduleEvent>();

    public virtual ICollection<StudyGroup> StudyGroups { get; set; } = new List<StudyGroup>();

    public virtual StudySession? StudySession { get; set; }

    public virtual ICollection<SystemSetting> SystemSettings { get; set; } = new List<SystemSetting>();

    public virtual UserSetting? UserSetting { get; set; }

    public virtual ICollection<UserSubject> UserSubjects { get; set; } = new List<UserSubject>();
}
