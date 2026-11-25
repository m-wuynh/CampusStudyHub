using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Pdf;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Pdf.Documents;

public sealed class PdfDocumentService(
    IRepository repository,
    ICurrentUser currentUser,
    IPdfFileStorage fileStorage)
{
    public async Task<long> UploadAsync(
        Stream source,
        string fileName,
        string? title,
        long? subjectId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var safeFileName = PdfInputRules.FileName(fileName);
        var fileNameKey = safeFileName.ToUpperInvariant();
        var documentTitle = PdfInputRules.Title(
            title,
            Path.GetFileNameWithoutExtension(safeFileName));

        await RequireSubjectAsync(
            userId,
            subjectId,
            cancellationToken);

        var documentCount = await repository.Query<PdfDocument>()
            .CountAsync(
                document => document.OwnerUserId == userId,
                cancellationToken);
        if (documentCount >= PdfPolicy.MaxDocumentsPerUser)
        {
            throw new PdfException(
                409,
                $"Mỗi tài khoản được lưu tối đa " +
                $"{PdfPolicy.MaxDocumentsPerUser} PDF.");
        }

        var storedFile = await fileStorage.SaveAsync(
            source,
            safeFileName,
            cancellationToken);

        try
        {
            await using var savedStream = await fileStorage.OpenReadAsync(
                storedFile.StorageKey,
                cancellationToken);

            var now = DateTime.UtcNow;
            var document = new PdfDocument
            {
                OwnerUserId = userId,
                UserSubjectId = subjectId,
                Title = documentTitle,
                OriginalFileName = storedFile.FileName,
                OriginalFileNameKey = fileNameKey,
                StorageKey = storedFile.StorageKey,
                PageCount = PdfInspection.CountPages(
                    savedStream,
                    cancellationToken),
                FileSizeBytes = storedFile.SizeBytes,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await repository.AddAsync(document, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            return document.DocumentId;
        }
        catch
        {
            await fileStorage.DeleteAsync(
                storedFile.StorageKey,
                CancellationToken.None);
            throw;
        }
    }

    public async Task<PdfReaderDto> GetReaderAsync(
        long documentId,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var document = await RequireDocumentAsync(
            documentId,
            cancellationToken);

        ValidatePage(pageNumber, document.PageCount);

        var notes = await repository.Query<PdfDocumentPageNote>()
            .Where(note =>
                note.OwnerUserId == userId &&
                note.DocumentId == documentId)
            .OrderBy(note => note.PhysicalPageNumber)
            .Select(note => new
            {
                Page = note.PhysicalPageNumber,
                note.ContentText
            })
            .ToListAsync(cancellationToken);

        var noteText = notes
            .FirstOrDefault(note => note.Page == pageNumber)
            ?.ContentText ?? string.Empty;

        var aiRequestsUsedToday = await repository.Query<PdfAiAnalysis>()
            .CountAsync(
                PdfPolicy.AiRequestsUsedToday(userId, DateTime.UtcNow),
                cancellationToken);

        return new PdfReaderDto(
            document.DocumentId,
            document.UserSubjectId,
            document.Title,
            document.OriginalFileName,
            document.PageCount,
            pageNumber,
            noteText,
            notes.Select(note => note.Page).ToList(),
            aiRequestsUsedToday,
            PdfPolicy.MaxAiRequestsPerDay);
    }

    public async Task RenameAsync(
        long documentId,
        string? title,
        CancellationToken cancellationToken = default)
    {
        var document = await RequireDocumentAsync(
            documentId,
            cancellationToken,
            trackChanges: true);

        document.Title = PdfInputRules.Title(title, document.Title);
        document.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<long?> DeleteAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await RequireDocumentAsync(
            documentId,
            cancellationToken,
            trackChanges: true);

        document.IsDeleted = true;
        document.DeletedAtUtc = DateTime.UtcNow;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        return document.UserSubjectId;
    }

    public async Task RestoreAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var document = await repository.Query<PdfDocument>(false)
            .SingleOrDefaultAsync(
                item =>
                    item.DocumentId == documentId &&
                    item.OwnerUserId == userId &&
                    item.IsDeleted,
                cancellationToken)
            ?? throw new PdfException(404, "Không tìm thấy PDF trong thùng rác.");

        if (document.UserSubjectId.HasValue)
        {
            var subjectIsActive = await repository.Query<UserSubject>()
                .AnyAsync(
                    subject =>
                        subject.UserSubjectId == document.UserSubjectId &&
                        subject.UserId == userId &&
                        !subject.IsArchived,
                    cancellationToken);

            if (!subjectIsActive)
            {
                document.UserSubjectId = null;
            }
        }

        document.IsDeleted = false;
        document.DeletedAtUtc = null;
        document.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PdfException(
                409,
                "PDF đã hết thời hạn lưu trong thùng rác.");
        }
    }

    public async Task<int> PurgeExpiredAsync(
        CancellationToken cancellationToken = default)
    {
        var cutoff = PdfPolicy.GetTrashCutoff(DateTime.UtcNow);
        var documents = await repository.Query<PdfDocument>(false)
            .Where(document =>
                document.IsDeleted &&
                document.DeletedAtUtc <= cutoff)
            .ToListAsync(cancellationToken);

        if (documents.Count == 0)
        {
            return 0;
        }

        var ids = documents.Select(item => item.DocumentId).ToList();
        var analyses = await repository.Query<PdfAiAnalysis>(false)
            .Where(item => ids.Contains(item.DocumentId))
            .ToListAsync(cancellationToken);
        var notes = await repository.Query<PdfDocumentPageNote>(false)
            .Where(item => ids.Contains(item.DocumentId))
            .ToListAsync(cancellationToken);

        repository.RemoveRange(analyses);
        repository.RemoveRange(notes);
        repository.RemoveRange(documents);
        await repository.SaveChangesAsync(cancellationToken);

        foreach (var document in documents)
        {
            await fileStorage.DeleteAsync(
                document.StorageKey,
                cancellationToken);
        }

        return documents.Count;
    }

    public async Task<Stream> OpenAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await RequireDocumentAsync(
            documentId,
            cancellationToken);

        return await fileStorage.OpenReadAsync(
            document.StorageKey,
            cancellationToken);
    }

    public async Task SaveNoteAsync(
        long documentId,
        int pageNumber,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var document = await RequireDocumentAsync(
            documentId,
            cancellationToken,
            trackChanges: true);

        ValidatePage(pageNumber, document.PageCount);

        var noteText = PdfInputRules.Note(content);
        var note = await repository.Query<PdfDocumentPageNote>(false)
            .SingleOrDefaultAsync(
                item =>
                    item.OwnerUserId == userId &&
                    item.DocumentId == documentId &&
                    item.PhysicalPageNumber == pageNumber,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(noteText))
        {
            if (note is not null)
            {
                repository.Remove(note);
            }
        }
        else if (note is null)
        {
            await repository.AddAsync(
                new PdfDocumentPageNote
                {
                    OwnerUserId = userId,
                    DocumentId = documentId,
                    PhysicalPageNumber = pageNumber,
                    ContentText = noteText,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                cancellationToken);
        }
        else
        {
            note.ContentText = noteText;
            note.UpdatedAtUtc = DateTime.UtcNow;
        }

        document.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    internal async Task<PdfDocument> RequireDocumentAsync(
        long documentId,
        CancellationToken cancellationToken,
        bool trackChanges = false)
    {
        var userId = currentUser.RequirePdfUserId();

        return await OwnedDocuments(userId, trackChanges)
            .SingleOrDefaultAsync(
                document => document.DocumentId == documentId,
                cancellationToken)
            ?? throw new PdfException(404, "Không tìm thấy PDF.");
    }

    private async Task RequireSubjectAsync(
        long userId,
        long? subjectId,
        CancellationToken cancellationToken)
    {
        if (!subjectId.HasValue)
        {
            return;
        }

        var subjectExists = await repository.Query<UserSubject>()
            .AnyAsync(
                subject =>
                    subject.UserSubjectId == subjectId.Value &&
                    subject.UserId == userId &&
                    !subject.IsArchived,
                cancellationToken);

        if (!subjectExists)
        {
            throw new PdfException(404, "Không tìm thấy môn học.");
        }
    }

    private IQueryable<PdfDocument> OwnedDocuments(
        long userId,
        bool trackChanges = false) =>
        repository.Query<PdfDocument>(asNoTracking: !trackChanges)
            .Where(document =>
                document.OwnerUserId == userId &&
                !document.IsDeleted);

    private static void ValidatePage(int pageNumber, int pageCount)
    {
        if (pageNumber < 1 || pageNumber > pageCount)
        {
            throw new PdfException(400, "Trang PDF không hợp lệ.");
        }
    }
}
