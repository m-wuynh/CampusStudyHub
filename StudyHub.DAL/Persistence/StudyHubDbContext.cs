using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;

namespace StudyHub.DAL.Persistence;

public partial class StudyHubDbContext : DbContext
{
    public StudyHubDbContext(DbContextOptions<StudyHubDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AcademicTerm> AcademicTerms { get; set; }

    public virtual DbSet<ActivityEvent> ActivityEvents { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<ContentReport> ContentReports { get; set; }

    public virtual DbSet<Deadline> Deadlines { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<DocumentBookmark> DocumentBookmarks { get; set; }

    public virtual DbSet<ExternalLogin> ExternalLogins { get; set; }

    public virtual DbSet<Flashcard> Flashcards { get; set; }

    public virtual DbSet<FlashcardDeck> FlashcardDecks { get; set; }

    public virtual DbSet<FlashcardProgress> FlashcardProgresses { get; set; }

    public virtual DbSet<FlashcardReview> FlashcardReviews { get; set; }

    public virtual DbSet<Goal> Goals { get; set; }

    public virtual DbSet<GradeEntry> GradeEntries { get; set; }

    public virtual DbSet<GroupMember> GroupMembers { get; set; }

    public virtual DbSet<GroupPost> GroupPosts { get; set; }

    public virtual DbSet<Note> Notes { get; set; }

    public virtual DbSet<Reminder> Reminders { get; set; }

    public virtual DbSet<ScheduleEvent> ScheduleEvents { get; set; }

    public virtual DbSet<ScheduleException> ScheduleExceptions { get; set; }

    public virtual DbSet<StudyGroup> StudyGroups { get; set; }

    public virtual DbSet<StudySession> StudySessions { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

    public virtual DbSet<UserSubject> UserSubjects { get; set; }

    public virtual DbSet<VwDailyUsageUtc> VwDailyUsageUtcs { get; set; }

    public virtual DbSet<VwStudyStreak> VwStudyStreaks { get; set; }

    public virtual DbSet<VwSubjectGradeSummary> VwSubjectGradeSummaries { get; set; }

    public virtual DbSet<VwTermGradeSummary> VwTermGradeSummaries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Latin1_General_100_CI_AS_SC");

        modelBuilder.Entity<AcademicTerm>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.TermName }, "UQ_AcademicTerms_Name").IsUnique();

            entity.HasIndex(e => new { e.AcademicTermId, e.UserId }, "UQ_AcademicTerms_Owner").IsUnique();

            entity.Property(e => e.TermName).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.AcademicTerms)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AcademicTerms_User");
        });

        modelBuilder.Entity<ActivityEvent>(entity =>
        {
            entity.HasIndex(e => new { e.OccurredAtUtc, e.UserId }, "IX_ActivityEvents_Analytics");

            entity.HasIndex(e => new { e.UserId, e.ActivityDate, e.IsStudyAction }, "IX_ActivityEvents_Streak");

            entity.HasIndex(e => e.RequestId, "UQ_ActivityEvents_Request").IsUnique();

            entity.Property(e => e.EventType)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.IsStudyAction).HasComputedColumnSql("(CONVERT([bit],case when [EventType]='DeadlineCompleted' OR [EventType]='FocusCompleted' OR [EventType]='FlashcardReviewed' OR [EventType]='NoteSaved' then (1) else (0) end))", true);
            entity.Property(e => e.OccurredAtUtc).HasPrecision(3);
            entity.Property(e => e.TimeZoneId).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.ActivityEvents)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ActivityEvents_User");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.CreatedAtUtc, "IX_AuditLogs_Date").IsDescending();

            entity.Property(e => e.ActionCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.EntityKey).HasMaxLength(100);
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Summary).HasMaxLength(1000);

            entity.HasOne(d => d.ActorUser).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorUserId)
                .HasConstraintName("FK_AuditLogs_Actor");
        });

        modelBuilder.Entity<ContentReport>(entity =>
        {
            entity.HasKey(e => e.ReportId);

            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc }, "IX_ContentReports_Queue");

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.ResolutionNote).HasMaxLength(1000);
            entity.Property(e => e.ResolvedAtUtc).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Open");

            entity.HasOne(d => d.Deck).WithMany(p => p.ContentReports)
                .HasForeignKey(d => d.DeckId)
                .HasConstraintName("FK_ContentReports_Deck");

            entity.HasOne(d => d.Document).WithMany(p => p.ContentReports)
                .HasForeignKey(d => d.DocumentId)
                .HasConstraintName("FK_ContentReports_Document");

            entity.HasOne(d => d.GroupPost).WithMany(p => p.ContentReports)
                .HasForeignKey(d => d.GroupPostId)
                .HasConstraintName("FK_ContentReports_Post");

            entity.HasOne(d => d.Note).WithMany(p => p.ContentReports)
                .HasForeignKey(d => d.NoteId)
                .HasConstraintName("FK_ContentReports_Note");

            entity.HasOne(d => d.ReporterUser).WithMany(p => p.ContentReportReporterUsers)
                .HasForeignKey(d => d.ReporterUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContentReports_Reporter");

            entity.HasOne(d => d.ResolvedByUser).WithMany(p => p.ContentReportResolvedByUsers)
                .HasForeignKey(d => d.ResolvedByUserId)
                .HasConstraintName("FK_ContentReports_Resolver");
        });

        modelBuilder.Entity<Deadline>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.IsDeleted, e.Status, e.DueAtUtc }, "IX_Deadlines_Due");

            entity.HasIndex(e => new { e.DeadlineId, e.UserId }, "UQ_Deadlines_Owner").IsUnique();

            entity.Property(e => e.CompletedAtUtc).HasPrecision(3);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.DueAtUtc).HasPrecision(3);
            entity.Property(e => e.Priority).HasDefaultValue((byte)2);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithMany(p => p.Deadlines)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Deadlines_User");

            entity.HasOne(d => d.UserSubject).WithMany(p => p.Deadlines)
                .HasPrincipalKey(p => new { p.UserSubjectId, p.UserId })
                .HasForeignKey(d => new { d.UserSubjectId, d.UserId })
                .HasConstraintName("FK_Deadlines_SubjectOwner");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasIndex(e => new { e.StudyGroupId, e.Visibility, e.IsDeleted }, "IX_Documents_Group");

            entity.HasIndex(e => new { e.OwnerUserId, e.IsDeleted, e.CreatedAtUtc }, "IX_Documents_Owner").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.UserSubjectId, e.Visibility, e.IsDeleted }, "IX_Documents_Subject");

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.ExternalUrl).HasMaxLength(2048);
            entity.Property(e => e.MimeType)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.OriginalFileName).HasMaxLength(255);
            entity.Property(e => e.ResourceType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.StorageKey).HasMaxLength(512);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Visibility)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Private");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.Documents)
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Documents_User");

            entity.HasOne(d => d.StudyGroup).WithMany(p => p.Documents)
                .HasForeignKey(d => d.StudyGroupId)
                .HasConstraintName("FK_Documents_Group");

            entity.HasOne(d => d.UserSubject).WithMany(p => p.Documents)
                .HasPrincipalKey(p => new { p.UserSubjectId, p.UserId })
                .HasForeignKey(d => new { d.UserSubjectId, d.OwnerUserId })
                .HasConstraintName("FK_Documents_SubjectOwner");
        });

        modelBuilder.Entity<DocumentBookmark>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.DocumentId });

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Document).WithMany(p => p.DocumentBookmarks)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DocumentBookmarks_Document");

            entity.HasOne(d => d.User).WithMany(p => p.DocumentBookmarks)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DocumentBookmarks_User");
        });

        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            entity.HasKey(e => new { e.Provider, e.ProviderSubject });

            entity.HasIndex(e => new { e.UserId, e.Provider }, "UQ_ExternalLogins_UserProvider").IsUnique();

            entity.Property(e => e.Provider)
                .HasMaxLength(20)
                .IsUnicode(false)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.ProviderSubject)
                .HasMaxLength(255)
                .IsUnicode(false)
                .UseCollation("Latin1_General_100_BIN2");
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LastLoginAtUtc).HasPrecision(3);

            entity.HasOne(d => d.User).WithMany(p => p.ExternalLogins)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ExternalLogins_User");
        });

        modelBuilder.Entity<Flashcard>(entity =>
        {
            entity.HasIndex(e => new { e.DeckId, e.IsDeleted, e.SortOrder }, "IX_Flashcards_Deck");

            entity.Property(e => e.BackText).HasMaxLength(4000);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FrontText).HasMaxLength(4000);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Deck).WithMany(p => p.Flashcards)
                .HasForeignKey(d => d.DeckId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Flashcards_Deck");
        });

        modelBuilder.Entity<FlashcardDeck>(entity =>
        {
            entity.HasKey(e => e.DeckId);

            entity.HasIndex(e => new { e.StudyGroupId, e.Visibility, e.IsDeleted }, "IX_FlashcardDecks_Group");

            entity.HasIndex(e => new { e.OwnerUserId, e.IsDeleted }, "IX_FlashcardDecks_Owner");

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Visibility)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Private");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.FlashcardDecks)
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FlashcardDecks_User");

            entity.HasOne(d => d.StudyGroup).WithMany(p => p.FlashcardDecks)
                .HasForeignKey(d => d.StudyGroupId)
                .HasConstraintName("FK_FlashcardDecks_Group");

            entity.HasOne(d => d.UserSubject).WithMany(p => p.FlashcardDecks)
                .HasPrincipalKey(p => new { p.UserSubjectId, p.UserId })
                .HasForeignKey(d => new { d.UserSubjectId, d.OwnerUserId })
                .HasConstraintName("FK_FlashcardDecks_SubjectOwner");
        });

        modelBuilder.Entity<FlashcardProgress>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.FlashcardId });

            entity.ToTable("FlashcardProgress");

            entity.HasIndex(e => new { e.UserId, e.NextReviewAtUtc }, "IX_FlashcardProgress_Due");

            entity.Property(e => e.EaseFactor)
                .HasDefaultValue(2.50m)
                .HasColumnType("decimal(4, 2)");
            entity.Property(e => e.LastReviewedAtUtc).HasPrecision(3);
            entity.Property(e => e.NextReviewAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Flashcard).WithMany(p => p.FlashcardProgresses)
                .HasForeignKey(d => d.FlashcardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FlashcardProgress_Card");

            entity.HasOne(d => d.User).WithMany(p => p.FlashcardProgresses)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FlashcardProgress_User");
        });

        modelBuilder.Entity<FlashcardReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId);

            entity.HasIndex(e => new { e.UserId, e.ReviewedAtUtc }, "IX_FlashcardReviews_History").IsDescending(false, true);

            entity.HasIndex(e => e.RequestId, "UQ_FlashcardReviews_Request").IsUnique();

            entity.Property(e => e.ReviewedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.FlashcardProgress).WithMany(p => p.FlashcardReviews)
                .HasForeignKey(d => new { d.UserId, d.FlashcardId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FlashcardReviews_Progress");
        });

        modelBuilder.Entity<Goal>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.IsCancelled, e.EndDate }, "IX_Goals_User");

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CurrentValue).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.TargetValue).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UnitCode)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithMany(p => p.Goals)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Goals_User");

            entity.HasOne(d => d.UserSubject).WithMany(p => p.Goals)
                .HasPrincipalKey(p => new { p.UserSubjectId, p.UserId })
                .HasForeignKey(d => new { d.UserSubjectId, d.UserId })
                .HasConstraintName("FK_Goals_SubjectOwner");
        });

        modelBuilder.Entity<GradeEntry>(entity =>
        {
            entity.HasIndex(e => new { e.UserSubjectId, e.IsDeleted }, "IX_GradeEntries_Subject");

            entity.Property(e => e.AssessmentType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Other");
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.MaxScore)
                .HasDefaultValue(10m)
                .HasColumnType("decimal(8, 2)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Score).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.Title).HasMaxLength(150);
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Weight)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(6, 2)");

            entity.HasOne(d => d.UserSubject).WithMany(p => p.GradeEntries)
                .HasForeignKey(d => d.UserSubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GradeEntries_Subject");
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => new { e.StudyGroupId, e.UserId });

            entity.HasIndex(e => new { e.UserId, e.Status, e.StudyGroupId }, "IX_GroupMembers_User");

            entity.Property(e => e.JoinedAtUtc).HasPrecision(3);
            entity.Property(e => e.MemberRole)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Member");
            entity.Property(e => e.RequestedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.StudyGroup).WithMany(p => p.GroupMembers)
                .HasForeignKey(d => d.StudyGroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GroupMembers_Group");

            entity.HasOne(d => d.User).WithMany(p => p.GroupMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GroupMembers_User");
        });

        modelBuilder.Entity<GroupPost>(entity =>
        {
            entity.HasIndex(e => new { e.StudyGroupId, e.IsDeleted, e.CreatedAtUtc }, "IX_GroupPosts_Feed").IsDescending(false, false, true);

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.GroupMember).WithMany(p => p.GroupPosts)
                .HasForeignKey(d => new { d.StudyGroupId, d.AuthorUserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GroupPosts_Member");
        });

        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasIndex(e => new { e.StudyGroupId, e.Visibility, e.IsDeleted }, "IX_Notes_Group");

            entity.HasIndex(e => new { e.OwnerUserId, e.IsDeleted, e.UpdatedAtUtc }, "IX_Notes_Owner").IsDescending(false, false, true);

            entity.Property(e => e.Body).HasDefaultValue("");
            entity.Property(e => e.BodyFormat)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Markdown");
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Visibility)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Private");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.Notes)
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notes_User");

            entity.HasOne(d => d.StudyGroup).WithMany(p => p.Notes)
                .HasForeignKey(d => d.StudyGroupId)
                .HasConstraintName("FK_Notes_Group");

            entity.HasOne(d => d.UserSubject).WithMany(p => p.Notes)
                .HasPrincipalKey(p => new { p.UserSubjectId, p.UserId })
                .HasForeignKey(d => new { d.UserSubjectId, d.OwnerUserId })
                .HasConstraintName("FK_Notes_SubjectOwner");
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.HasIndex(e => new { e.Status, e.RemindAtUtc }, "IX_Reminders_Queue");

            entity.HasIndex(e => new { e.UserId, e.DeadlineId, e.Channel, e.RemindAtUtc }, "UX_Reminders_Deadline")
                .IsUnique()
                .HasFilter("([DeadlineId] IS NOT NULL)");

            entity.HasIndex(e => new { e.UserId, e.ScheduleEventId, e.OccurrenceDate, e.Channel, e.RemindAtUtc }, "UX_Reminders_Event")
                .IsUnique()
                .HasFilter("([ScheduleEventId] IS NOT NULL)");

            entity.Property(e => e.Channel)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("InApp");
            entity.Property(e => e.LastError).HasMaxLength(1000);
            entity.Property(e => e.RemindAtUtc).HasPrecision(3);
            entity.Property(e => e.SentAtUtc).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.User).WithMany(p => p.Reminders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reminders_User");

            entity.HasOne(d => d.Deadline).WithMany(p => p.Reminders)
                .HasPrincipalKey(p => new { p.DeadlineId, p.UserId })
                .HasForeignKey(d => new { d.DeadlineId, d.UserId })
                .HasConstraintName("FK_Reminders_DeadlineOwner");

            entity.HasOne(d => d.ScheduleEvent).WithMany(p => p.Reminders)
                .HasPrincipalKey(p => new { p.ScheduleEventId, p.UserId })
                .HasForeignKey(d => new { d.ScheduleEventId, d.UserId })
                .HasConstraintName("FK_Reminders_EventOwner");
        });

        modelBuilder.Entity<ScheduleEvent>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.IsDeleted, e.StartAtLocal }, "IX_ScheduleEvents_Calendar");

            entity.HasIndex(e => new { e.ScheduleEventId, e.UserId }, "UQ_ScheduleEvents_Owner").IsUnique();

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.EndAtLocal).HasPrecision(0);
            entity.Property(e => e.EventType)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Class");
            entity.Property(e => e.Location).HasMaxLength(250);
            entity.Property(e => e.RepeatMode)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("None");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.StartAtLocal).HasPrecision(0);
            entity.Property(e => e.TimeZoneId)
                .HasMaxLength(100)
                .HasDefaultValue("SE Asia Standard Time");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithMany(p => p.ScheduleEvents)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ScheduleEvents_User");

            entity.HasOne(d => d.UserSubject).WithMany(p => p.ScheduleEvents)
                .HasPrincipalKey(p => new { p.UserSubjectId, p.UserId })
                .HasForeignKey(d => new { d.UserSubjectId, d.UserId })
                .HasConstraintName("FK_ScheduleEvents_SubjectOwner");
        });

        modelBuilder.Entity<ScheduleException>(entity =>
        {
            entity.HasKey(e => new { e.ScheduleEventId, e.OccurrenceDate });

            entity.Property(e => e.NewEndAtLocal).HasPrecision(0);
            entity.Property(e => e.NewLocation).HasMaxLength(250);
            entity.Property(e => e.NewStartAtLocal).HasPrecision(0);

            entity.HasOne(d => d.ScheduleEvent).WithMany(p => p.ScheduleExceptions)
                .HasForeignKey(d => d.ScheduleEventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ScheduleExceptions_Event");
        });

        modelBuilder.Entity<StudyGroup>(entity =>
        {
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.GroupName).HasMaxLength(150);
            entity.Property(e => e.MaxMembers).HasDefaultValue((short)20);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Visibility)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Private");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.StudyGroups)
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StudyGroups_Owner");

            entity.HasOne(d => d.Subject).WithMany(p => p.StudyGroups)
                .HasForeignKey(d => d.SubjectId)
                .HasConstraintName("FK_StudyGroups_Subject");
        });

        modelBuilder.Entity<StudySession>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.StartedAtUtc }, "IX_StudySessions_User").IsDescending(false, true);

            entity.HasIndex(e => e.UserId, "UX_StudySessions_Running")
                .IsUnique()
                .HasFilter("([Status]='Running')");

            entity.Property(e => e.EndedAtUtc).HasPrecision(3);
            entity.Property(e => e.StartedAtUtc).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Running");

            entity.HasOne(d => d.User).WithOne(p => p.StudySession)
                .HasForeignKey<StudySession>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StudySessions_User");

            entity.HasOne(d => d.UserSubject).WithMany(p => p.StudySessions)
                .HasPrincipalKey(p => new { p.UserSubjectId, p.UserId })
                .HasForeignKey(d => new { d.UserSubjectId, d.UserId })
                .HasConstraintName("FK_StudySessions_SubjectOwner");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasIndex(e => e.SubjectCode, "UQ_Subjects_Code").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SubjectCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.SubjectName).HasMaxLength(150);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingKey);

            entity.Property(e => e.SettingKey)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SettingValue).HasMaxLength(2000);
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.UpdatedByUser).WithMany(p => p.SystemSettings)
                .HasForeignKey(d => d.UpdatedByUserId)
                .HasConstraintName("FK_SystemSettings_User");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "IX_Users_Email");

            entity.Property(e => e.AvatarUrl).HasMaxLength(2048);
            entity.Property(e => e.ClassName).HasMaxLength(100);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.EducationLevel)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("University");
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.RoleCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Student");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SchoolName).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");
            entity.Property(e => e.StudentCode).HasMaxLength(50);
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.DailyStudyGoalMinutes).HasDefaultValue((short)30);
            entity.Property(e => e.DefaultReminderMinutes).HasDefaultValue((short)15);
            entity.Property(e => e.InAppRemindersEnabled).HasDefaultValue(true);
            entity.Property(e => e.LanguageCode)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("vi");
            entity.Property(e => e.Theme)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("System");
            entity.Property(e => e.TimeZoneId)
                .HasMaxLength(100)
                .HasDefaultValue("SE Asia Standard Time");
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.User).WithOne(p => p.UserSetting)
                .HasForeignKey<UserSetting>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSettings_User");
        });

        modelBuilder.Entity<UserSubject>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.AcademicTermId, e.SubjectId }, "UQ_UserSubjects_Enrollment").IsUnique();

            entity.HasIndex(e => new { e.UserSubjectId, e.UserId }, "UQ_UserSubjects_Owner").IsUnique();

            entity.Property(e => e.ClassCode).HasMaxLength(50);
            entity.Property(e => e.ColorHex)
                .HasMaxLength(7)
                .IsUnicode(false)
                .HasDefaultValue("#5138EE")
                .IsFixedLength();
            entity.Property(e => e.CreditWeight)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.TargetScore10).HasColumnType("decimal(4, 2)");
            entity.Property(e => e.TeacherName).HasMaxLength(100);

            entity.HasOne(d => d.Subject).WithMany(p => p.UserSubjects)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSubjects_Subject");

            entity.HasOne(d => d.User).WithMany(p => p.UserSubjects)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSubjects_User");

            entity.HasOne(d => d.AcademicTerm).WithMany(p => p.UserSubjects)
                .HasPrincipalKey(p => new { p.AcademicTermId, p.UserId })
                .HasForeignKey(d => new { d.AcademicTermId, d.UserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSubjects_TermOwner");
        });

        modelBuilder.Entity<VwDailyUsageUtc>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_DailyUsageUtc");
        });

        modelBuilder.Entity<VwStudyStreak>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_StudyStreaks");
        });

        modelBuilder.Entity<VwSubjectGradeSummary>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_SubjectGradeSummary");

            entity.Property(e => e.CreditWeight).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.CurrentAverage10).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.SubjectName).HasMaxLength(150);
            entity.Property(e => e.TargetScore10).HasColumnType("decimal(4, 2)");
        });

        modelBuilder.Entity<VwTermGradeSummary>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TermGradeSummary");

            entity.Property(e => e.CurrentAverage10).HasColumnType("decimal(5, 2)");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
