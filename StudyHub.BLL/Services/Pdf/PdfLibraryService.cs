using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Pdf;

public sealed class PdfLibraryService(
    IRepository repository,
    ICurrentUser currentUser)
{
    private const string UncategorizedName = "Chưa phân loại";
    private const string UncategorizedColor = "#64748B";
    public async Task<PdfLibraryDto> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var documents = OwnedDocuments(userId);

        var folders = await repository.Query<UserSubject>()
            .Where(subject =>
                subject.UserId == userId &&
                !subject.IsArchived)
            .OrderBy(subject => subject.DisplayOrder)
            .Select(subject => new PdfSubjectFolderDto(
                subject.UserSubjectId,
                subject.CustomSubjectName ?? subject.Subject!.SubjectName,
                subject.ColorHex,
                documents.Count(document =>
                    document.UserSubjectId == subject.UserSubjectId)))
            .ToListAsync(cancellationToken);

        var uncategorizedCount = await documents.CountAsync(
            document => document.UserSubjectId == null,
            cancellationToken);

        folders.Insert(0, new PdfSubjectFolderDto(
            null,
            UncategorizedName,
            UncategorizedColor,
            uncategorizedCount));

        var documentCount = await documents.CountAsync(cancellationToken);
        var recentDocuments = await LoadDocumentsAsync(
            documents,
            cancellationToken,
            take: 3);

        return new PdfLibraryDto(
            documentCount,
            folders,
            recentDocuments);
    }

    public async Task<PdfFolderDetailsDto> GetFolderAsync(
        long? subjectId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var folderName = UncategorizedName;
        var folderColor = UncategorizedColor;

        if (subjectId.HasValue)
        {
            var folder = await repository.Query<UserSubject>()
                .Where(subject =>
                    subject.UserSubjectId == subjectId.Value &&
                    subject.UserId == userId &&
                    !subject.IsArchived)
                .Select(subject => new
                {
                    Name = subject.CustomSubjectName ??
                           subject.Subject!.SubjectName,
                    subject.ColorHex
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (folder is null)
            {
                throw NotFound("Không tìm thấy môn học.");
            }

            folderName = folder.Name;
            folderColor = folder.ColorHex;
        }

        var documents = OwnedDocuments(userId)
            .Where(document => document.UserSubjectId == subjectId);

        return new PdfFolderDetailsDto(
            subjectId,
            folderName,
            folderColor,
            await LoadDocumentsAsync(documents, cancellationToken));
    }

    public async Task<IReadOnlyList<PdfTrashItemDto>> GetTrashAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();

        return await repository.Query<PdfDocument>()
            .Where(document =>
                document.OwnerUserId == userId &&
                document.IsDeleted &&
                document.DeletedAtUtc != null)
            .OrderByDescending(document => document.DeletedAtUtc)
            .Select(document => new PdfTrashItemDto(
                document.DocumentId,
                document.Title,
                document.OriginalFileName,
                document.DeletedAtUtc!.Value,
                PdfPolicy.GetTrashExpiration(
                    document.DeletedAtUtc.Value)))
            .ToListAsync(cancellationToken);
    }

    public async Task<PdfDashboardDto> GetDashboardAsync(
        long? subjectId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var subjectName = UncategorizedName;
        if (subjectId.HasValue)
        {
            subjectName = await repository.Query<UserSubject>()
                .Where(subject =>
                    subject.UserSubjectId == subjectId.Value &&
                    subject.UserId == userId &&
                    !subject.IsArchived)
                .Select(subject =>
                    subject.CustomSubjectName ??
                    subject.Subject!.SubjectName)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("Không tìm thấy môn học.");
        }

        var documents = OwnedDocuments(userId)
            .Where(document => document.UserSubjectId == subjectId);

        var documentIds = documents
            .Select(document => document.DocumentId);

        var analyses = repository.Query<PdfAiAnalysis>()
            .Where(analysis =>
                analysis.OwnerUserId == userId &&
                documentIds.Contains(analysis.DocumentId));

        var documentCount = await documents.CountAsync(cancellationToken);

        var notePageCount = await repository.Query<PdfDocumentPageNote>()
            .CountAsync(note =>
                note.OwnerUserId == userId &&
                documentIds.Contains(note.DocumentId),
                cancellationToken);

        var acceptedAnalysisCount = await analyses.CountAsync(
            analysis => analysis.Status == PdfAiAnalysis.Accepted,
            cancellationToken);

        var inputTokens = await analyses.SumAsync(
            analysis => (int?)analysis.InputTokens,
            cancellationToken) ?? 0;

        var outputTokens = await analyses.SumAsync(
            analysis => (int?)analysis.OutputTokens,
            cancellationToken) ?? 0;

        var aiRequestsUsedToday = await repository.Query<PdfAiAnalysis>()
            .CountAsync(
                PdfPolicy.AiRequestsUsedToday(userId, DateTime.UtcNow),
                cancellationToken);

        return new PdfDashboardDto(
            subjectId,
            subjectName,
            documentCount,
            notePageCount,
            acceptedAnalysisCount,
            aiRequestsUsedToday,
            PdfPolicy.MaxAiRequestsPerDay,
            inputTokens,
            outputTokens);
    }

    private IQueryable<PdfDocument> OwnedDocuments(long userId) =>
        repository.Query<PdfDocument>()
            .Where(document =>
                document.OwnerUserId == userId &&
                !document.IsDeleted);

    private static async Task<IReadOnlyList<PdfDocumentItemDto>>
        LoadDocumentsAsync(
            IQueryable<PdfDocument> query,
            CancellationToken cancellationToken,
            int? take = null)
    {
        IQueryable<PdfDocument> documents = query
            .OrderByDescending(document => document.UpdatedAtUtc)
            .ThenByDescending(document => document.DocumentId);

        if (take.HasValue)
        {
            documents = documents.Take(take.Value);
        }

        return await documents
            .Select(document => new PdfDocumentItemDto(
                document.DocumentId,
                document.UserSubject == null
                    ? null
                    : document.UserSubject.CustomSubjectName ??
                      document.UserSubject.Subject!.SubjectName,
                document.Title,
                document.OriginalFileName,
                document.PageCount))
            .ToListAsync(cancellationToken);
    }

    private static PdfException NotFound(string message) =>
        new(404, message);
}
