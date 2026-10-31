using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.NotesV3;

namespace StudyHub.DAL.Persistence;

public partial class StudyHubDbContext
{
    public DbSet<NotesV3Notebook> NotesV3Notebooks =>
        Set<NotesV3Notebook>();

    public DbSet<NotesV3Section> NotesV3Sections =>
        Set<NotesV3Section>();

    public DbSet<NotesV3Page> NotesV3Pages =>
        Set<NotesV3Page>();

    public DbSet<NotesV3Document> NotesV3Documents =>
        Set<NotesV3Document>();

    public DbSet<NotesV3DocumentVersion> NotesV3DocumentVersions =>
        Set<NotesV3DocumentVersion>();

    public DbSet<NotesV3DocumentShare> NotesV3DocumentShares =>
        Set<NotesV3DocumentShare>();

    public DbSet<NotesV3PdfPageLink> NotesV3PdfPageLinks =>
        Set<NotesV3PdfPageLink>();

    public DbSet<NotesV3Reference> NotesV3References =>
        Set<NotesV3Reference>();

    public DbSet<NotesV3Annotation> NotesV3Annotations =>
        Set<NotesV3Annotation>();

    public DbSet<NotesV3CollabUpdate> NotesV3CollabUpdates =>
        Set<NotesV3CollabUpdate>();

    private static void ConfigureNotesV3(ModelBuilder modelBuilder)
    {
        ConfigureNotesV3Notebooks(modelBuilder);
        ConfigureNotesV3Sections(modelBuilder);
        ConfigureNotesV3Pages(modelBuilder);

        ConfigureNotesV3Documents(modelBuilder);
        ConfigureNotesV3Versions(modelBuilder);
        ConfigureNotesV3Shares(modelBuilder);

        ConfigureNotesV3PdfPageLinks(modelBuilder);
        ConfigureNotesV3References(modelBuilder);
        ConfigureNotesV3Annotations(modelBuilder);
        ConfigureNotesV3Updates(modelBuilder);
    }

    private static void ConfigureNotesV3Notebooks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3Notebook>();

        entity.ToTable("NotesV3Notebooks", table =>
        {
            table.HasCheckConstraint(
                "CK_NotesV3Notebooks_SortOrder",
                "[SortOrder] >= 0");
        });

        entity.HasKey(x => x.NotebookId);

        entity.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasIndex(x => new
        {
            x.OwnerUserId,
            x.IsDeleted,
            x.SortOrder
        });

        entity.HasIndex(x => new
        {
            x.StudyGroupId,
            x.IsDeleted,
            x.SortOrder
        });

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne<StudyGroup>()
            .WithMany()
            .HasForeignKey(x => x.StudyGroupId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureNotesV3Sections(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3Section>();

        entity.ToTable("NotesV3Sections", table =>
        {
            table.HasCheckConstraint(
                "CK_NotesV3Sections_SortOrder",
                "[SortOrder] >= 0");
        });

        entity.HasKey(x => x.SectionId);

        entity.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        entity.HasIndex(x => new
        {
            x.NotebookId,
            x.IsDeleted,
            x.SortOrder
        });

        entity.HasOne<NotesV3Notebook>()
            .WithMany()
            .HasForeignKey(x => x.NotebookId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureNotesV3Pages(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3Page>();

        entity.ToTable("NotesV3Pages", table =>
        {
            table.HasCheckConstraint(
                "CK_NotesV3Pages_SortOrder",
                "[SortOrder] >= 0");

            table.HasCheckConstraint(
                "CK_NotesV3Pages_DeltaJson",
                "ISJSON([ContentDeltaJson]) = 1");
        });

        entity.HasKey(x => x.PageId);

        entity.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(x => x.ContentDeltaJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        entity.Property(x => x.RowVersion)
            .IsRowVersion();

        entity.HasIndex(x => new
        {
            x.SectionId,
            x.IsDeleted,
            x.SortOrder
        });

        entity.HasOne<NotesV3Section>()
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureNotesV3Documents(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3Document>();

        entity.ToTable("NotesV3Documents");
        entity.HasKey(x => x.DocumentId);

        entity.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.Property(x => x.UpdatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.Property(x => x.RowVersion)
            .IsRowVersion();

        entity.HasIndex(x => new
        {
            x.OwnerUserId,
            x.IsDeleted,
            x.IsArchived,
            x.UpdatedAtUtc
        });

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureNotesV3Versions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3DocumentVersion>();

        entity.ToTable("NotesV3DocumentVersions", table =>
        {
            table.HasCheckConstraint(
                "CK_NotesV3DocumentVersions_FileSize",
                "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 26214400");

            table.HasCheckConstraint(
                "CK_NotesV3DocumentVersions_PageCount",
                "[PageCount] IS NULL OR [PageCount] >= 1");
        });

        entity.HasKey(x => x.DocumentVersionId);

        entity.Property(x => x.StorageKey)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(x => x.OriginalFileName)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(x => x.Sha256)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();

        entity.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasIndex(x => x.StorageKey)
            .IsUnique();

        entity.HasIndex(x => new
        {
            x.DocumentId,
            x.DocumentVersionId
        });

        entity.HasOne<NotesV3Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureNotesV3Shares(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3DocumentShare>();

        entity.ToTable("NotesV3DocumentShares", table =>
        {
            table.HasCheckConstraint(
                "CK_NotesV3DocumentShares_State",
                """
                ([IsActive] = 1 AND [UnsharedAtUtc] IS NULL)
                OR
                ([IsActive] = 0 AND [UnsharedAtUtc] IS NOT NULL)
                """);
        });

        entity.HasKey(x => x.GroupDocumentShareId);

        entity.Property(x => x.SharedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.Property(x => x.RowVersion)
            .IsRowVersion();

        // Một PDF chỉ có một session đang hoạt động trong cùng nhóm.
        entity.HasIndex(x => new
            {
                x.DocumentId,
                x.StudyGroupId
            })
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        entity.HasIndex(x => new
        {
            x.StudyGroupId,
            x.IsActive
        });

        entity.HasOne<NotesV3Document>()
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

    private static void ConfigureNotesV3PdfPageLinks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3PdfPageLink>();

        entity.ToTable("NotesV3PdfPageLinks", table =>
        {
            table.HasCheckConstraint(
                "CK_NotesV3PdfPageLinks_Scope",
                """
                ([OwnerUserId] IS NOT NULL AND [GroupDocumentShareId] IS NULL)
                OR
                ([OwnerUserId] IS NULL AND [GroupDocumentShareId] IS NOT NULL)
                """);

            table.HasCheckConstraint(
                "CK_NotesV3PdfPageLinks_PhysicalPage",
                "[PhysicalPageNumber] >= 1");
        });

        entity.HasKey(x => x.PageId);

        entity.Property(x => x.PageId)
            .ValueGeneratedNever();

        entity.HasOne<NotesV3Page>()
            .WithOne()
            .HasForeignKey<NotesV3PdfPageLink>(x => x.PageId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne<NotesV3DocumentVersion>()
            .WithMany()
            .HasForeignKey(x => x.DocumentVersionId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne<NotesV3DocumentShare>()
            .WithMany()
            .HasForeignKey(x => x.GroupDocumentShareId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasIndex(x => new
            {
                x.OwnerUserId,
                x.DocumentVersionId,
                x.PhysicalPageNumber
            })
            .IsUnique()
            .HasFilter("[OwnerUserId] IS NOT NULL");

        entity.HasIndex(x => new
            {
                x.GroupDocumentShareId,
                x.DocumentVersionId,
                x.PhysicalPageNumber
            })
            .IsUnique()
            .HasFilter("[GroupDocumentShareId] IS NOT NULL");
    }

    private static void ConfigureNotesV3References(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3Reference>();

        entity.ToTable("NotesV3References", table =>
        {
            table.HasCheckConstraint(
                "CK_NotesV3References_PhysicalPage",
                "[PhysicalPageNumber] >= 1");

            table.HasCheckConstraint(
                "CK_NotesV3References_AnchorJson",
                "[AnchorJson] IS NULL OR ISJSON([AnchorJson]) = 1");
        });

        entity.HasKey(x => x.ReferenceId);

        entity.Property(x => x.AnchorJson)
            .HasColumnType("nvarchar(max)");

        entity.Property(x => x.CapturedText)
            .HasMaxLength(4000)
            .IsRequired();

        entity.HasIndex(x => x.PageId);

        entity.HasOne<NotesV3Page>()
            .WithMany()
            .HasForeignKey(x => x.PageId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne<NotesV3DocumentVersion>()
            .WithMany()
            .HasForeignKey(x => x.DocumentVersionId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne<NotesV3DocumentShare>()
            .WithMany()
            .HasForeignKey(x => x.GroupDocumentShareId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureNotesV3Annotations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3Annotation>();

        entity.ToTable("NotesV3Annotations", table =>
        {
            table.HasCheckConstraint(
                "CK_NotesV3Annotations_Scope",
                """
                ([OwnerUserId] IS NOT NULL AND [GroupDocumentShareId] IS NULL)
                OR
                ([OwnerUserId] IS NULL AND [GroupDocumentShareId] IS NOT NULL)
                """);

            table.HasCheckConstraint(
                "CK_NotesV3Annotations_PhysicalPage",
                "[PhysicalPageNumber] >= 1");

            table.HasCheckConstraint(
                "CK_NotesV3Annotations_PayloadJson",
                "ISJSON([PayloadJson]) = 1");
        });

        entity.HasKey(x => x.AnnotationId);

        entity.Property(x => x.PayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        entity.Property(x => x.RowVersion)
            .IsRowVersion();

        entity.HasIndex(x => new
        {
            x.OwnerUserId,
            x.DocumentVersionId,
            x.PhysicalPageNumber,
            x.IsDeleted
        });

        entity.HasIndex(x => new
        {
            x.GroupDocumentShareId,
            x.DocumentVersionId,
            x.PhysicalPageNumber,
            x.IsDeleted
        });

        entity.HasOne<NotesV3DocumentVersion>()
            .WithMany()
            .HasForeignKey(x => x.DocumentVersionId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne<NotesV3DocumentShare>()
            .WithMany()
            .HasForeignKey(x => x.GroupDocumentShareId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureNotesV3Updates(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotesV3CollabUpdate>();

        entity.ToTable("NotesV3CollabUpdates", table =>
        {
            table.HasCheckConstraint(
                "CK_NotesV3CollabUpdates_Bytes",
                "DATALENGTH([UpdateBytes]) BETWEEN 1 AND 262144");

            table.HasCheckConstraint(
                "CK_NotesV3CollabUpdates_Operation",
                "[OperationId] <> '00000000-0000-0000-0000-000000000000'");
        });

        entity.HasKey(x => x.UpdateId);

        entity.Property(x => x.UpdateBytes)
            .HasColumnType("varbinary(max)")
            .IsRequired();

        entity.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasIndex(x => new
            {
                x.PageId,
                x.OperationId
            })
            .IsUnique();

        entity.HasIndex(x => new
        {
            x.PageId,
            x.UpdateId
        });

        entity.HasOne<NotesV3Page>()
            .WithMany()
            .HasForeignKey(x => x.PageId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}