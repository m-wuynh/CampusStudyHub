using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Access;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.DAL.Entities.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class PdfServiceTests
{
    [Fact]
    public async Task Upload_RejectsDuplicateOriginalFileNameIncludingTrash()
    {
        await using var test = new PdfTestContext();
        test.Db.PdfDocuments.Add(Document(
            ownerId: 1,
            fileName: "lecture.pdf",
            isDeleted: true));
        await test.Db.SaveChangesAsync();
        var storage = new FakePdfStorage();
        var service = CreateService(test, storage);

        var error = await Assert.ThrowsAsync<PdfException>(() =>
            service.UploadAsync(
                new MemoryStream(PdfTestData.OnePagePdf()),
                "lecture.pdf",
                null));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal(0, storage.SaveCalls);
    }

    [Fact]
    public async Task Delete_RequiresAllActiveGroupSharesToBeRevokedFirst()
    {
        await using var test = new PdfTestContext();
        var document = Document(ownerId: 1, fileName: "shared.pdf");
        test.Db.Add(document);
        await test.Db.SaveChangesAsync();
        test.Db.PdfDocumentShares.Add(new PdfDocumentShare
        {
            DocumentId = document.DocumentId,
            StudyGroupId = 10,
            SharedByUserId = 1,
            IsActive = true,
            SharedAtUtc = DateTime.UtcNow
        });
        await test.Db.SaveChangesAsync();
        var service = CreateService(test, new FakePdfStorage());

        var error = await Assert.ThrowsAsync<PdfException>(() =>
            service.DeleteAsync(
                document.DocumentId,
                Convert.ToBase64String(document.RowVersion)));

        Assert.Equal(409, error.StatusCode);
        Assert.False((await test.Db.PdfDocuments.SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task DeleteAndRestore_ChangeOnlyTheSoftDeleteState()
    {
        await using var test = new PdfTestContext();
        var document = Document(ownerId: 1, fileName: "owned.pdf");
        test.Db.Add(document);
        await test.Db.SaveChangesAsync();
        var storage = new FakePdfStorage();
        var service = CreateService(test, storage);
        var token = Convert.ToBase64String(document.RowVersion);

        await service.DeleteAsync(document.DocumentId, token);
        test.Db.ChangeTracker.Clear();
        var deleted = await test.Db.PdfDocuments.SingleAsync();
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAtUtc);
        Assert.Empty(storage.DeletedKeys);

        await service.RestoreAsync(
            deleted.DocumentId,
            Convert.ToBase64String(deleted.RowVersion));
        test.Db.ChangeTracker.Clear();
        var restored = await test.Db.PdfDocuments.SingleAsync();
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedAtUtc);
        Assert.Empty(storage.DeletedKeys);
    }

    private static PdfService CreateService(
        PdfTestContext test,
        IPdfFileStorage storage)
    {
        var access = new PdfAccessService(test.Repository, test.CurrentUser);
        return new PdfService(
            test.Repository,
            test.CurrentUser,
            access,
            storage,
            NullLogger<PdfService>.Instance);
    }

    internal static PdfDocument Document(
        long ownerId,
        string fileName,
        bool isDeleted = false) => new()
    {
        OwnerUserId = ownerId,
        Title = Path.GetFileNameWithoutExtension(fileName),
        StorageKey = $"{Guid.NewGuid():N}.pdf",
        OriginalFileName = fileName,
        OriginalFileNameKey = fileName,
        PageCount = 3,
        FileSizeBytes = 1024,
        IsDeleted = isDeleted,
        DeletedAtUtc = isDeleted ? DateTime.UtcNow.AddDays(-1) : null,
        CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
        UpdatedAtUtc = DateTime.UtcNow,
        RowVersion = [1, 2, 3, 4, 5, 6, 7, 8]
    };
}
