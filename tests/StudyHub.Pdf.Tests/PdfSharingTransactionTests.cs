using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.Pdf.Sharing;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class PdfSharingTransactionTests
{
    [Fact]
    public async Task Share_ReusesAnExistingCallerTransaction()
    {
        await using var test = new PdfTestContext();
        test.Repository.HasActiveTransaction = true;
        var document = PdfServiceTests.Document(1, "shared.pdf");
        test.Db.Add(document);
        await test.Db.SaveChangesAsync();
        var service = new PdfSharingService(
            test.Repository,
            test.CurrentUser,
            new PdfShareStore(test.Db));

        await service.ShareAsync(document.DocumentId, 10, 1);

        Assert.Equal(0, test.Repository.BeginTransactionCalls);
        Assert.True(await test.Db.PdfDocumentShares.AnyAsync(
            share => share.DocumentId == document.DocumentId &&
                     share.StudyGroupId == 10 &&
                     share.IsActive));
    }

    [Fact]
    public async Task Share_StartsAndOwnsATransactionWhenCalledIndependently()
    {
        await using var test = new PdfTestContext();
        var document = PdfServiceTests.Document(1, "standalone.pdf");
        test.Db.Add(document);
        await test.Db.SaveChangesAsync();
        var service = new PdfSharingService(
            test.Repository,
            test.CurrentUser,
            new PdfShareStore(test.Db));

        await service.ShareAsync(document.DocumentId, 10, 1);

        Assert.Equal(1, test.Repository.BeginTransactionCalls);
        Assert.Single(await test.Db.PdfDocumentShares.ToListAsync());
    }
}
