using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.Pdf;

namespace StudyHub.DAL.Persistence;

public partial class StudyHubDbContext
{
    public DbSet<PdfDocument> PdfDocuments => Set<PdfDocument>();

    public DbSet<PdfDocumentPageNote> PdfDocumentPageNotes =>
        Set<PdfDocumentPageNote>();

    public DbSet<PdfAiAnalysis> PdfAiAnalyses => Set<PdfAiAnalysis>();

    private static void ConfigurePdf(ModelBuilder modelBuilder)
    {
        ConfigurePdfDocument(modelBuilder);
        ConfigurePageNote(modelBuilder);
        ConfigureAiAnalysis(modelBuilder);
    }

    private static void ConfigurePdfDocument(ModelBuilder modelBuilder)
    {
        var document = modelBuilder.Entity<PdfDocument>();

        document.ToTable("PdfDocuments", table =>
        {
            table.HasCheckConstraint(
                "CK_PdfDocuments_PageCount",
                "[PageCount] >= 1");

            table.HasCheckConstraint(
                "CK_PdfDocuments_FileSizeBytes",
                "[FileSizeBytes] > 0");
        });

        document.HasKey(item => item.DocumentId);

        document.Property(item => item.Title)
            .HasMaxLength(PdfDocument.MaxTitleLength)
            .IsRequired();

        document.Property(item => item.OriginalFileName)
            .HasMaxLength(PdfDocument.MaxFileNameLength)
            .IsRequired();

        document.Property(item => item.OriginalFileNameKey)
            .HasMaxLength(PdfDocument.MaxFileNameLength)
            .IsRequired();

        document.Property(item => item.StorageKey)
            .HasMaxLength(PdfDocument.MaxStorageKeyLength)
            .IsRequired();

        document.Property(item => item.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        document.Property(item => item.UpdatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        document.Property(item => item.RowVersion)
            .IsRowVersion();

        document.HasIndex(item => item.StorageKey)
            .IsUnique();

        document.HasIndex(item => new
            {
                item.OwnerUserId,
                item.OriginalFileNameKey
            });

        document.HasIndex(item => new
        {
            item.OwnerUserId,
            item.UserSubjectId,
            item.IsDeleted,
            item.UpdatedAtUtc
        });

        document.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        document.HasOne(item => item.UserSubject)
            .WithMany(subject => subject.PdfDocuments)
            .HasPrincipalKey(subject => new
            {
                subject.UserSubjectId,
                subject.UserId
            })
            .HasForeignKey(item => new
            {
                item.UserSubjectId,
                item.OwnerUserId
            })
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigurePageNote(ModelBuilder modelBuilder)
    {
        var note = modelBuilder.Entity<PdfDocumentPageNote>();

        note.ToTable("PdfDocumentPageNotes", table =>
        {
            table.HasCheckConstraint(
                "CK_PdfDocumentPageNotes_PhysicalPage",
                "[PhysicalPageNumber] >= 1");
        });

        note.HasKey(item => item.PdfDocumentPageNoteId);

        note.Property(item => item.ContentText)
            .HasMaxLength(PdfDocumentPageNote.MaxContentLength);

        note.Property(item => item.UpdatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        note.Property(item => item.RowVersion)
            .IsRowVersion();

        note.HasIndex(item => new
            {
                item.OwnerUserId,
                item.DocumentId,
                item.PhysicalPageNumber
            })
            .IsUnique();

        note.HasOne<PdfDocument>()
            .WithMany()
            .HasForeignKey(item => item.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        note.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureAiAnalysis(ModelBuilder modelBuilder)
    {
        var analysis = modelBuilder.Entity<PdfAiAnalysis>();

        analysis.ToTable("PdfAiAnalyses", table =>
        {
            table.HasCheckConstraint(
                "CK_PdfAiAnalyses_Pages",
                "[FromPage] >= 1 AND [ToPage] >= [FromPage]");

            table.HasCheckConstraint(
                "CK_PdfAiAnalyses_ResultJson",
                "ISJSON([ResultJson]) = 1");
        });

        analysis.HasKey(item => item.AnalysisId);

        analysis.Property(item => item.SourceHash)
            .HasMaxLength(64)
            .IsUnicode(false);

        analysis.Property(item => item.Model)
            .HasMaxLength(100);

        analysis.Property(item => item.PromptVersion)
            .HasMaxLength(20)
            .IsUnicode(false);

        analysis.Property(item => item.Status)
            .HasMaxLength(12)
            .IsUnicode(false);

        analysis.Property(item => item.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        analysis.Property(item => item.UpdatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        analysis.Property(item => item.RowVersion)
            .IsRowVersion();

        analysis.HasIndex(item => new
        {
            item.OwnerUserId,
            item.DocumentId,
            item.FromPage,
            item.ToPage,
            item.SourceHash,
            item.Model,
            item.PromptVersion
        })
            .IsUnique()
            .HasFilter($"[Status] <> '{PdfAiAnalysis.Discarded}'");

        analysis.HasOne<PdfDocument>()
            .WithMany()
            .HasForeignKey(item => item.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        analysis.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

}
