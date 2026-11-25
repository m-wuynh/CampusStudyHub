using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;

namespace StudyHub.DAL.Persistence;

public partial class StudyHubDbContext
{
    public DbSet<CalendarCategory> CalendarCategories => Set<CalendarCategory>();

    internal static void ConfigureCalendar(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarCategory>(e =>
        {
            e.HasKey(x => x.CategoryId);
            e.ToTable("CalendarCategories");
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.CreatedAtUtc).HasPrecision(3);
            e.Property(x => x.UpdatedAtUtc).HasPrecision(3);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
