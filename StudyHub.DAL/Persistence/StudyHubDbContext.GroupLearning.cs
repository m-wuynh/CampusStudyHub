using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;

namespace StudyHub.DAL.Persistence;

public partial class StudyHubDbContext
{
    public DbSet<GroupStudySession> GroupStudySessions => Set<GroupStudySession>();
    public DbSet<GroupSessionAttendance> GroupSessionAttendances => Set<GroupSessionAttendance>();
    public DbSet<GroupAssignment> GroupAssignments => Set<GroupAssignment>();
    public DbSet<GroupAssignmentAssignee> GroupAssignmentAssignees => Set<GroupAssignmentAssignee>();
    public DbSet<GroupSubmission> GroupSubmissions => Set<GroupSubmission>();
    public DbSet<GroupQuestion> GroupQuestions => Set<GroupQuestion>();
    public DbSet<GroupAnswer> GroupAnswers => Set<GroupAnswer>();
    public DbSet<GroupWeeklyGoal> GroupWeeklyGoals => Set<GroupWeeklyGoal>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GroupStudySession>(e =>
        {
            e.ToTable("GroupStudySessions", t =>
            {
                t.HasCheckConstraint("CK_GroupStudySessions_Time", "EndsAtUtc > StartsAtUtc");
                t.HasCheckConstraint("CK_GroupStudySessions_Status", "Status IN ('Scheduled','Completed','Cancelled')");
            });
            e.HasIndex(x => new { x.StudyGroupId, x.StartsAtUtc });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Objective).HasMaxLength(1000);
            e.Property(x => x.Preparation).HasMaxLength(4000);
            e.Property(x => x.Location).HasMaxLength(300);
            e.Property(x => x.MeetingUrl).HasMaxLength(2000);
            e.Property(x => x.Summary).HasMaxLength(4000);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.StudyGroupId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<GroupSessionAttendance>(e =>
        {
            e.HasKey(x => new { x.SessionId, x.UserId });
            e.HasOne(x => x.Session).WithMany(x => x.Attendees).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<GroupAssignment>(e =>
        {
            e.ToTable("GroupAssignments", t => t.HasCheckConstraint("CK_GroupAssignments_Status", "Status IN ('Open','Closed')"));
            e.HasIndex(x => new { x.StudyGroupId, x.DueAtUtc });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(8000);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.StudyGroupId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<GroupStudySession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<GroupAssignmentAssignee>(e =>
        {
            e.HasKey(x => new { x.AssignmentId, x.UserId });
            e.HasOne(x => x.Assignment).WithMany(x => x.Assignees).HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<GroupSubmission>(e =>
        {
            e.ToTable("GroupSubmissions", t => t.HasCheckConstraint("CK_GroupSubmissions_Status", "Status IN ('Submitted','NeedsRevision','Completed')"));
            e.HasIndex(x => new { x.AssignmentId, x.UserId, x.Version }).IsUnique();
            e.Property(x => x.Content).HasMaxLength(12000);
            e.Property(x => x.ResourceUrl).HasMaxLength(2000);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Feedback).HasMaxLength(4000);
            e.HasOne(x => x.Assignee).WithMany(x => x.Submissions).HasForeignKey(x => new { x.AssignmentId, x.UserId }).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<GroupQuestion>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => new { x.StudyGroupId, x.CreatedAtUtc });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Subject).HasMaxLength(150);
            e.Property(x => x.Body).HasMaxLength(8000);
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.StudyGroupId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.NoAction);
            // Composite FK ensures the selected answer belongs to this question.
            e.HasOne<GroupAnswer>().WithMany().HasForeignKey(x => new { x.Id, x.AcceptedAnswerId })
                .HasPrincipalKey(x => new { x.QuestionId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<GroupAnswer>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(8000);
            e.HasOne(x => x.Question).WithMany(x => x.Answers).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<GroupWeeklyGoal>(e =>
        {
            e.ToTable("GroupWeeklyGoals", t => t.HasCheckConstraint("CK_GroupWeeklyGoals_Target", "TargetSessions BETWEEN 0 AND 100 AND TargetAssignments BETWEEN 0 AND 100 AND TargetSessions + TargetAssignments > 0"));
            e.HasIndex(x => new { x.StudyGroupId, x.WeekStart }).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.StudyGroupId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
