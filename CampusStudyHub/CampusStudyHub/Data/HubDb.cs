using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CampusStudyHub.Data;

public class Student
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Avatar { get; set; } = "";
    [Required, MaxLength(150)] public string University { get; set; } = "";
    [Required, MaxLength(150)] public string Faculty { get; set; } = "";
    [Required, MaxLength(40)] public string Cohort { get; set; } = "";
    public bool IsAdmin { get; set; }
    public bool Suspended { get; set; }
}
public class Subject
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = "";
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(120)] public string Lecturer { get; set; } = "";
    [MaxLength(60)] public string ClassName { get; set; } = "";
    [RegularExpression("^#[0-9a-fA-F]{6}$")] public string Color { get; set; } = "#6366f1";
    public string InviteCode { get; set; } = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
    public bool Archived { get; set; }
}
public class Membership
{
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public string StudentId { get; set; } = "";
    public Student Student { get; set; } = null!;
}
public class StudyItem
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = "";
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public string Kind { get; set; } = "deadline";
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string Notes { get; set; } = "";
    public DateTime When { get; set; } = DateTime.Today.AddDays(1).AddHours(9);
    [Range(0.25, 12)] public double Hours { get; set; } = 1.5;
    [MaxLength(2048)] public string Url { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string DocumentType { get; set; } = "Bài giảng";
    [Range(2, 50)] public int Capacity { get; set; } = 5;
    public bool Done { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Hidden { get; set; }
    public int Views { get; set; }
    public int Downloads { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Bookmark
{
    public int ItemId { get; set; }
    public StudyItem Item { get; set; } = null!;
    public string StudentId { get; set; } = "";
}
public class GroupRequest
{
    public int ItemId { get; set; }
    public StudyItem Item { get; set; } = null!;
    public string StudentId { get; set; } = "";
    public string Status { get; set; } = "pending";
}
public class Report
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public StudyItem Item { get; set; } = null!;
    public string StudentId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Resolution { get; set; } = "";
    public string? ResolvedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class ActivityEvent
{
    public int Id { get; set; }
    public string StudentId { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
public class HubDb(DbContextOptions<HubDb> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<StudyItem> Items => Set<StudyItem>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<GroupRequest> GroupRequests => Set<GroupRequest>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ActivityEvent> Events => Set<ActivityEvent>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Membership>().HasKey(x => new { x.SubjectId, x.StudentId });
        b.Entity<Bookmark>().HasKey(x => new { x.ItemId, x.StudentId });
        b.Entity<GroupRequest>().HasKey(x => new { x.ItemId, x.StudentId });
        b.Entity<Subject>().HasIndex(x => x.InviteCode).IsUnique();
        b.Entity<StudyItem>().HasIndex(x => new { x.SubjectId, x.Kind });
        b.Entity<ActivityEvent>().HasIndex(x => new { x.StudentId, x.At });
    }
}
