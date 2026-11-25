using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class PdfLibraryServiceTests
{
    [Fact]
    public async Task GetAsync_IncludesEmptySubjectFolder()
    {
        await using var test = new PdfTestContext();
        test.Db.UserSubjects.Add(Subject(10, 1, "Lập trình Web"));
        await test.Db.SaveChangesAsync();

        var result = await Service(test).GetAsync();

        var folder = Assert.Single(
            result.Folders,
            item => item.UserSubjectId == 10);
        Assert.Equal("Lập trình Web", folder.Name);
        Assert.Equal(0, folder.DocumentCount);
    }

    [Fact]
    public async Task GetAsync_GroupsOwnedDocumentsBySubject()
    {
        await using var test = new PdfTestContext();
        test.Db.UserSubjects.Add(Subject(10, 1, "Cơ sở dữ liệu"));
        test.Db.PdfDocuments.AddRange(
            Document(100, 1, 10, "database.pdf"),
            Document(101, 1, null, "unclassified.pdf"),
            Document(102, 2, null, "other-user.pdf"));
        await test.Db.SaveChangesAsync();

        var result = await Service(test).GetAsync();

        Assert.Equal(2, result.DocumentCount);
        Assert.Equal(2, result.RecentDocuments.Count);
        Assert.Equal(1, Assert.Single(
            result.Folders,
            item => item.UserSubjectId == 10).DocumentCount);
        Assert.Equal(1, Assert.Single(
            result.Folders,
            item => item.UserSubjectId == null).DocumentCount);
        Assert.DoesNotContain(
            result.RecentDocuments,
            item => item.DocumentId == 102);
    }

    [Fact]
    public async Task GetFolderAsync_ReturnsOnlyRequestedFolder()
    {
        await using var test = new PdfTestContext();
        test.Db.UserSubjects.AddRange(
            Subject(10, 1, "Lập trình Web"),
            Subject(11, 1, "Cơ sở dữ liệu"));
        test.Db.PdfDocuments.AddRange(
            Document(100, 1, 10, "web.pdf"),
            Document(101, 1, 11, "database.pdf"),
            Document(102, 1, null, "unclassified.pdf"));
        await test.Db.SaveChangesAsync();

        var result = await Service(test).GetFolderAsync(10);

        var document = Assert.Single(result.Documents);
        Assert.Equal(10, result.UserSubjectId);
        Assert.Equal("Lập trình Web", result.Name);
        Assert.Equal(100, document.DocumentId);
    }

    [Fact]
    public async Task GetFolderAsync_HidesAnotherUsersSubject()
    {
        await using var test = new PdfTestContext(userId: 1);
        test.Db.UserSubjects.Add(Subject(20, 2, "Môn của người khác"));
        await test.Db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PdfException>(
            () => Service(test).GetFolderAsync(20));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task GetDashboardAsync_ShowsTodaysAiUsageForCurrentUser()
    {
        await using var test = new PdfTestContext();
        test.Db.PdfDocuments.AddRange(
            Document(100, 1, null, "lesson.pdf"),
            Document(102, 2, null, "other-user.pdf"));
        test.Db.PdfAiAnalyses.AddRange(
            Analysis(10, 1, 100, PdfAiAnalysis.Accepted),
            Analysis(11, 2, 102, PdfAiAnalysis.Accepted));
        await test.Db.SaveChangesAsync();

        var result = await Service(test).GetDashboardAsync(null);

        Assert.Equal(1, result.AiRequestsUsedToday);
        Assert.Equal(PdfPolicy.MaxAiRequestsPerDay, result.AiDailyLimit);
        Assert.Equal(1, result.AcceptedAnalysisCount);
    }

    private static PdfLibraryService Service(PdfTestContext test)
    {
        return new PdfLibraryService(test.Repository, test.CurrentUser);
    }

    private static PdfAiAnalysis Analysis(
        long id,
        long ownerUserId,
        long documentId,
        string status) =>
        new()
        {
            AnalysisId = id,
            OwnerUserId = ownerUserId,
            DocumentId = documentId,
            FromPage = 1,
            ToPage = 1,
            SourceHash = id.ToString().PadLeft(64, 'A'),
            Model = "gemini-test",
            PromptVersion = "v1",
            Status = status,
            ResultJson = "{}",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static UserSubject Subject(long id, long userId, string name) =>
        new()
        {
            UserSubjectId = id,
            UserId = userId,
            CustomSubjectName = name,
            CreditWeight = 1,
            ColorHex = "#5138EE",
            DisplayOrder = 1
        };

    private static PdfDocument Document(
        long id,
        long ownerUserId,
        long? userSubjectId,
        string fileName) =>
        new()
        {
            DocumentId = id,
            OwnerUserId = ownerUserId,
            UserSubjectId = userSubjectId,
            Title = Path.GetFileNameWithoutExtension(fileName),
            OriginalFileName = fileName,
            OriginalFileNameKey = fileName.ToUpperInvariant(),
            StorageKey = $"{Guid.NewGuid():N}.pdf",
            PageCount = 3,
            FileSizeBytes = 1024,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow,
            RowVersion = [1, 2, 3, 4, 5, 6, 7, 8]
        };
}
