using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.Pdf;

namespace StudyHub.DAL.Persistence;

public partial class StudyHubDbContext
{
    public DbSet<PdfDocument> PdfDocuments => Set<PdfDocument>();
    public DbSet<PdfDocumentShare> PdfDocumentShares => Set<PdfDocumentShare>();
    public DbSet<PdfDocumentPageNote> PdfDocumentPageNotes => Set<PdfDocumentPageNote>();
    public DbSet<PdfAnnotation> PdfAnnotations => Set<PdfAnnotation>();

    private static void ConfigurePdf(ModelBuilder modelBuilder)
    {
        ConfigurePdfDocuments(modelBuilder);
        ConfigurePdfShares(modelBuilder);
        ConfigurePdfDocumentPageNotes(modelBuilder);
        ConfigurePdfAnnotations(modelBuilder);
    }

    private static void ConfigurePdfDocuments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PdfDocument>();

        entity.ToTable("PdfDocuments", table =>
        {
            table.HasCheckConstraint(
                "CK_PdfDocuments_PageCount",
                "[PageCount] >= 1");
            table.HasCheckConstraint(
                "CK_PdfDocuments_FileSizeBytes",
                "[FileSizeBytes] >= 0");
        });
        entity.HasKey(x => x.DocumentId);

        entity.Property(x => x.Title).HasMaxLength(200);
        entity.Property(x => x.StorageKey).HasMaxLength(200);
        entity.Property(x => x.OriginalFileName).HasMaxLength(200);
        entity.Property(x => x.OriginalFileNameKey).HasMaxLength(200);
        entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasIndex(x => x.StorageKey).IsUnique();
        entity.HasIndex(x => new { x.OwnerUserId, x.OriginalFileNameKey })
            .IsUnique()
            .HasFilter("[OriginalFileNameKey] IS NOT NULL");
        entity.HasIndex(x => new
        {
            x.OwnerUserId,
            x.IsDeleted,
            x.UpdatedAtUtc
        });

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigurePdfShares(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PdfDocumentShare>();

        entity.ToTable("PdfDocumentShares", table =>
            table.HasCheckConstraint(
                "CK_PdfDocumentShares_State",
                """
                ([IsActive] = 1 AND [UnsharedAtUtc] IS NULL)
                OR
                ([IsActive] = 0 AND [UnsharedAtUtc] IS NOT NULL)
                """));
        entity.HasKey(x => x.PdfDocumentShareId);
        entity.Property(x => x.SharedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasIndex(x => new { x.DocumentId, x.StudyGroupId })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
        entity.HasIndex(x => new { x.StudyGroupId, x.IsActive });

        entity.HasOne<PdfDocument>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<StudyGroup>()
            .WithMany()
            .HasForeignKey(x => x.StudyGroupId)
            .OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.SharedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigurePdfDocumentPageNotes(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PdfDocumentPageNote>();

        entity.ToTable("PdfDocumentPageNotes", table =>
            table.HasCheckConstraint(
                "CK_PdfDocumentPageNotes_PhysicalPage",
                "[PhysicalPageNumber] >= 1"));
        entity.HasKey(x => x.PdfDocumentPageNoteId);
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasIndex(x => new
        {
            x.OwnerUserId,
            x.DocumentId,
            x.PhysicalPageNumber
        }).IsUnique();

        entity.HasOne<PdfDocument>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigurePdfAnnotations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PdfAnnotation>();

        entity.ToTable("PdfAnnotations", table =>
        {
            table.HasCheckConstraint(
                "CK_PdfAnnotations_PhysicalPage",
                "[PhysicalPageNumber] >= 1");
            table.HasCheckConstraint(
                "CK_PdfAnnotations_PayloadJson",
                "ISJSON([PayloadJson]) = 1");
        });
        entity.HasKey(x => x.AnnotationId);
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => new
        {
            x.OwnerUserId,
            x.DocumentId,
            x.PhysicalPageNumber
        });

        entity.HasOne<PdfDocument>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

}
