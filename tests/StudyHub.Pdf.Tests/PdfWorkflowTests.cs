using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Ai;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.DAL.Entities.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class PdfWorkflowTests
{
    [Fact]
    public async Task Upload_CreatesClassifiedDocument()
    {
        await using var test = new PdfTestContext();
        var storage = new FakePdfStorage();
        var service = new PdfDocumentService(test.Repository, test.CurrentUser, storage);

        await using var pdfStream = new MemoryStream(PdfTestData.OnePagePdf());
        var documentId = await service.UploadAsync(
            pdfStream,
            "lesson.pdf",
            title: null,
            subjectId: null);

        var document = await test.Db.PdfDocuments
            .SingleAsync(item => item.DocumentId == documentId);

        Assert.Equal("lesson", document.Title);
        Assert.Equal(1, document.PageCount);
        Assert.Equal(1, storage.SaveCalls);
    }

    [Fact]
    public async Task Upload_AllowsDuplicateFileNames()
    {
        await using var test = new PdfTestContext();
        var storage = new FakePdfStorage();
        var service = new PdfDocumentService(
            test.Repository,
            test.CurrentUser,
            storage);

        await using var first = new MemoryStream(PdfTestData.OnePagePdf());
        await using var second = new MemoryStream(PdfTestData.OnePagePdf());

        await service.UploadAsync(first, "lesson.pdf", null, null);
        await service.UploadAsync(second, "lesson.pdf", null, null);

        Assert.Equal(2, await test.Db.PdfDocuments.CountAsync());
        Assert.Equal(2, storage.SaveCalls);
    }

    [Fact]
    public async Task Upload_CountsTrashDocumentsTowardQuota()
    {
        await using var test = new PdfTestContext();

        for (var id = 1; id <= PdfPolicy.MaxDocumentsPerUser; id++)
        {
            var document = Document();
            document.DocumentId = id;
            document.StorageKey = $"{id:D32}.pdf";
            document.IsDeleted = true;
            document.DeletedAtUtc = DateTime.UtcNow;
            test.Db.PdfDocuments.Add(document);
        }

        await test.Db.SaveChangesAsync();
        var storage = new FakePdfStorage();
        var service = new PdfDocumentService(
            test.Repository,
            test.CurrentUser,
            storage);
        await using var source = new MemoryStream(PdfTestData.OnePagePdf());

        var exception = await Assert.ThrowsAsync<PdfException>(() =>
            service.UploadAsync(source, "new.pdf", null, null));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(0, storage.SaveCalls);
    }

    [Fact]
    public async Task SaveNote_UpsertsThenRemovesEmptyNote()
    {
        await using var test = new PdfTestContext();
        test.Db.PdfDocuments.Add(Document());
        await test.Db.SaveChangesAsync();
        var service = new PdfDocumentService(test.Repository, test.CurrentUser, new FakePdfStorage());

        await service.SaveNoteAsync(1, 1, "Điểm quan trọng");
        var savedNote = await test.Db.PdfDocumentPageNotes.SingleAsync();
        var reader = await service.GetReaderAsync(1, 1);
        Assert.Equal("Điểm quan trọng", savedNote.ContentText);
        Assert.Equal([1], reader.NotePages);
        Assert.Equal(0, reader.AiRequestsUsedToday);
        Assert.Equal(PdfPolicy.MaxAiRequestsPerDay, reader.AiDailyLimit);

        await service.SaveNoteAsync(1, 1, "  ");
        Assert.Empty(await test.Db.PdfDocumentPageNotes.ToListAsync());
    }

    [Fact]
    public async Task Rename_ChangesOnlyDocumentTitle()
    {
        await using var test = new PdfTestContext();
        var document = Document();
        document.UserSubjectId = 8;
        test.Db.PdfDocuments.Add(document);
        await test.Db.SaveChangesAsync();

        var service = new PdfDocumentService(
            test.Repository,
            test.CurrentUser,
            new FakePdfStorage());

        await service.RenameAsync(1, "New title");

        document = await test.Db.PdfDocuments.SingleAsync();
        Assert.Equal("New title", document.Title);
        Assert.Equal(8, document.UserSubjectId);
    }

    [Fact]
    public async Task Delete_MovesDocumentToTrash()
    {
        await using var test = new PdfTestContext();
        test.Db.PdfDocuments.Add(Document());
        test.Db.PdfAiAnalyses.Add(new PdfAiAnalysis
        {
            AnalysisId = 2,
            OwnerUserId = 1,
            DocumentId = 1,
            FromPage = 1,
            ToPage = 1,
            SourceHash = new string('A', 64),
            Model = "fake-gemini",
            PromptVersion = "v1",
            Status = PdfAiAnalysis.Accepted,
            ResultJson = "{}"
        });
        test.Db.PdfDocumentPageNotes.Add(new PdfDocumentPageNote
        {
            OwnerUserId = 1,
            DocumentId = 1,
            PhysicalPageNumber = 1,
            ContentText = "note"
        });
        await test.Db.SaveChangesAsync();

        var storage = new FakePdfStorage();
        var service = new PdfDocumentService(
            test.Repository,
            test.CurrentUser,
            storage);

        await service.DeleteAsync(1);

        var document = await test.Db.PdfDocuments.SingleAsync();
        Assert.True(document.IsDeleted);
        Assert.NotNull(document.DeletedAtUtc);
        Assert.Empty(storage.DeletedKeys);
        Assert.Single(await test.Db.PdfDocumentPageNotes.ToListAsync());
        Assert.Single(await test.Db.PdfAiAnalyses.ToListAsync());

        await service.RestoreAsync(1);

        document = await test.Db.PdfDocuments.SingleAsync();
        Assert.False(document.IsDeleted);
        Assert.Null(document.DeletedAtUtc);
    }

    [Fact]
    public async Task PurgeExpired_DeletesDocumentAndStoredFile()
    {
        await using var test = new PdfTestContext();
        var document = Document();
        document.IsDeleted = true;
        document.DeletedAtUtc = DateTime.UtcNow.AddDays(-31);
        test.Db.PdfDocuments.Add(document);
        await test.Db.SaveChangesAsync();

        var storage = new FakePdfStorage
        {
            BeforeDeleteAsync = _ =>
            {
                Assert.Empty(test.Db.PdfDocuments);
                return Task.CompletedTask;
            }
        };
        var service = new PdfDocumentService(
            test.Repository,
            test.CurrentUser,
            storage);

        var deleted = await service.PurgeExpiredAsync();

        Assert.Equal(1, deleted);
        Assert.Empty(await test.Db.PdfDocuments.ToListAsync());
        Assert.Contains(document.StorageKey, storage.DeletedKeys);
    }

    [Fact]
    public async Task Restore_MovesDocumentFromArchivedSubjectToUncategorized()
    {
        await using var test = new PdfTestContext();
        test.Db.UserSubjects.Add(new StudyHub.DAL.Entities.UserSubject
        {
            UserSubjectId = 8,
            UserId = 1,
            CustomSubjectName = "Archived subject",
            ColorHex = "#123456",
            IsArchived = true
        });

        var document = Document();
        document.UserSubjectId = 8;
        document.IsDeleted = true;
        document.DeletedAtUtc = DateTime.UtcNow;
        test.Db.PdfDocuments.Add(document);
        await test.Db.SaveChangesAsync();

        var service = new PdfDocumentService(
            test.Repository,
            test.CurrentUser,
            new FakePdfStorage());

        await service.RestoreAsync(1);

        Assert.Null((await test.Db.PdfDocuments.SingleAsync()).UserSubjectId);
    }

    [Fact]
    public async Task AcceptAnalysis_AppendsFullResultToPageNote()
    {
        await using var test = new PdfTestContext();
        test.Db.PdfDocuments.Add(Document());
        test.Db.PdfAiAnalyses.Add(new PdfAiAnalysis
        {
            AnalysisId = 9,
            OwnerUserId = 1,
            DocumentId = 1,
            FromPage = 1,
            ToPage = 1,
            SourceHash = new string('A', 64),
            Model = "fake-gemini",
            PromptVersion = "v1",
            Status = PdfAiAnalysis.Draft,
            ResultJson = "{\"summary\":\"cũ\",\"keyPoints\":[],\"concepts\":[]}",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        test.Db.PdfDocumentPageNotes.Add(new PdfDocumentPageNote
        {
            OwnerUserId = 1,
            DocumentId = 1,
            PhysicalPageNumber = 1,
            ContentText = "Manual note"
        });
        await test.Db.SaveChangesAsync();

        var storage = new FakePdfStorage();
        var documentService = new PdfDocumentService(
            test.Repository,
            test.CurrentUser,
            storage);
        var service = new PdfAiService(
            test.Repository,
            test.CurrentUser,
            documentService,
            storage,
            new FakePdfAiClient());
        var editedResult = new PdfAiResult(
            "Bản đã sửa",
            ["Ý chính"],
            [new PdfAiConcept(
                "SOLID",
                "Nguyên tắc thiết kế",
                "S là gì?",
                "Single responsibility")]);

        await service.AcceptAsync(9, editedResult);

        var analysis = await test.Db.PdfAiAnalyses.SingleAsync();
        var note = await test.Db.PdfDocumentPageNotes.SingleAsync();

        Assert.Equal(PdfAiAnalysis.Accepted, analysis.Status);
        Assert.Contains("Manual note", note.ContentText);
        Assert.Contains("[AI tổng hợp trang 1]", note.ContentText);
        Assert.Contains("Bản đã sửa", note.ContentText);
        Assert.Contains("Ý chính", note.ContentText);
        Assert.Contains("SOLID", note.ContentText);
        Assert.Contains("Nguyên tắc thiết kế", note.ContentText);
        Assert.Contains("S là gì?", note.ContentText);
        Assert.Contains("Single responsibility", note.ContentText);

        var updatedResult = editedResult with { Summary = "Bản cập nhật" };
        await service.AcceptAsync(9, updatedResult);

        Assert.Contains("Manual note", note.ContentText);
        Assert.DoesNotContain("Bản đã sửa", note.ContentText);
        Assert.Contains("Bản cập nhật", note.ContentText);
        Assert.Equal(
            1,
            note.ContentText.Split("[AI tổng hợp trang 1]").Length - 1);
    }

    [Fact]
    public async Task DiscardAnalysis_ChangesStatus()
    {
        await using var test = new PdfTestContext();
        test.Db.PdfDocuments.Add(Document());
        test.Db.PdfAiAnalyses.Add(Analysis(PdfAiAnalysis.Draft));
        await test.Db.SaveChangesAsync();

        var storage = new FakePdfStorage();
        var documents = new PdfDocumentService(
            test.Repository, test.CurrentUser, storage);
        var service = new PdfAiService(
            test.Repository, test.CurrentUser, documents, storage,
            new FakePdfAiClient());

        await service.DiscardAsync(9);

        Assert.Equal(PdfAiAnalysis.Discarded,
            (await test.Db.PdfAiAnalyses.SingleAsync()).Status);
    }

    [Fact]
    public async Task AcceptAnalysis_RejectsDiscardedResult()
    {
        await using var test = new PdfTestContext();
        test.Db.PdfDocuments.Add(Document());
        test.Db.PdfAiAnalyses.Add(Analysis(PdfAiAnalysis.Discarded));
        await test.Db.SaveChangesAsync();

        var storage = new FakePdfStorage();
        var documents = new PdfDocumentService(
            test.Repository, test.CurrentUser, storage);
        var service = new PdfAiService(
            test.Repository, test.CurrentUser, documents, storage,
            new FakePdfAiClient());
        var result = new PdfAiResult(
            "Summary", ["Point"],
            [new PdfAiConcept("Title", "Summary", "Question", "Answer")]);

        var exception = await Assert.ThrowsAsync<PdfException>(() =>
            service.AcceptAsync(9, result));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task AcceptAnalysis_RejectsEmptySummary()
    {
        await using var test = new PdfTestContext();
        var storage = new FakePdfStorage();
        var documents = new PdfDocumentService(
            test.Repository, test.CurrentUser, storage);
        var service = new PdfAiService(
            test.Repository, test.CurrentUser, documents, storage,
            new FakePdfAiClient());
        var result = new PdfAiResult(
            "", [],
            [new PdfAiConcept("Title", "Summary", "Question", "Answer")]);

        var exception = await Assert.ThrowsAsync<PdfException>(() =>
            service.AcceptAsync(9, result));

        Assert.Equal(400, exception.StatusCode);
    }

    private static PdfAiAnalysis Analysis(string status) =>
        new()
        {
            AnalysisId = 9,
            OwnerUserId = 1,
            DocumentId = 1,
            FromPage = 1,
            ToPage = 1,
            SourceHash = new string('A', 64),
            Model = "fake-gemini",
            PromptVersion = "v1",
            Status = status,
            ResultJson = "{\"summary\":\"old\",\"keyPoints\":[],\"concepts\":[]}",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static PdfDocument Document()
    {
        return new PdfDocument
        {
            DocumentId = 1,
            OwnerUserId = 1,
            Title = "Lesson",
            OriginalFileName = "lesson.pdf",
            OriginalFileNameKey = "LESSON.PDF",
            StorageKey = "00000000000000000000000000000000.pdf",
            PageCount = 1,
            FileSizeBytes = 500,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
