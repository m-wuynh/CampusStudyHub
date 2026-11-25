using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Ai;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.DAL.Entities.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class PdfSystemTests
{
    [Fact]
    public async Task StudentCanUploadReadNoteTrashAndRestorePdf()
    {
        await using var test = new PdfTestContext();
        var storage = new FakePdfStorage();
        var documents = new PdfDocumentService(
            test.Repository, test.CurrentUser, storage);

        await using var source = new MemoryStream(
            PdfTestData.OnePagePdf());
        var documentId = await documents.UploadAsync(
            source,
            "lecture.pdf",
            "Lecture 01",
            null);

        var reader = await documents.GetReaderAsync(documentId, 1);
        Assert.Equal("Lecture 01", reader.Title);
        Assert.Empty(reader.NotePages);

        await documents.SaveNoteAsync(
            documentId,
            1,
            "Important definition");

        reader = await documents.GetReaderAsync(documentId, 1);
        Assert.Equal("Important definition", reader.NoteText);
        Assert.Contains(1, reader.NotePages);

        await documents.DeleteAsync(documentId);
        Assert.True((await test.Db.PdfDocuments.SingleAsync()).IsDeleted);

        await documents.RestoreAsync(documentId);
        Assert.False((await test.Db.PdfDocuments.SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task StudentCanCreateAiDraftReviewAndAcceptIt()
    {
        await using var test = new PdfTestContext();
        var document = new PdfDocument
        {
            DocumentId = 1,
            OwnerUserId = 1,
            Title = "Lecture",
            OriginalFileName = "lecture.pdf",
            OriginalFileNameKey = "LECTURE.PDF",
            StorageKey = "lecture.pdf",
            PageCount = 1,
            FileSizeBytes = 100,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        test.Db.PdfDocuments.Add(document);
        await test.Db.SaveChangesAsync();

        var storage = new FakePdfStorage();
        var documents = new PdfDocumentService(
            test.Repository, test.CurrentUser, storage);
        var ai = new FakePdfAiClient();
        var service = new PdfAiService(
            test.Repository,
            test.CurrentUser,
            documents,
            storage,
            ai);

        // The fake storage contains a one-page PDF without text, so seed a
        // draft to test the review and approval part of the system flow.
        var draft = new PdfAiAnalysis
        {
            AnalysisId = 1,
            OwnerUserId = 1,
            DocumentId = 1,
            FromPage = 1,
            ToPage = 1,
            SourceHash = new string('A', 64),
            Model = ai.Model,
            PromptVersion = "v1",
            Status = PdfAiAnalysis.Draft,
            ResultJson = "{\"summary\":\"Draft\",\"keyPoints\":[],\"concepts\":[{\"title\":\"Concept\",\"summary\":\"Meaning\",\"question\":\"What?\",\"answer\":\"This\"}]}",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        test.Db.PdfAiAnalyses.Add(draft);
        await test.Db.SaveChangesAsync();

        var review = await service.GetAsync(1);
        Assert.True(review.IsDraft);

        var result = new PdfAiResult(
            "Approved summary",
            ["Approved point"],
            [new PdfAiConcept(
                "Concept",
                "Meaning",
                "What?",
                "This")]);
        await service.AcceptAsync(1, result);

        var analysis = await test.Db.PdfAiAnalyses.SingleAsync();
        var note = await test.Db.PdfDocumentPageNotes.SingleAsync();
        Assert.Equal(PdfAiAnalysis.Accepted, analysis.Status);
        Assert.Contains("Approved summary", note.ContentText);
    }
}
