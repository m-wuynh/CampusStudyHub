using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.Web.Configuration;
using StudyHub.Web.Controllers.Pdf.Documents;

namespace StudyHub.Pdf.Tests;

public sealed class PdfLibraryAndContractTests
{
    [Fact]
    public async Task Trash_ShowsOnlyRecoverableDocumentsFromLastThirtyDays()
    {
        await using var test = new PdfTestContext();
        var recent = PdfServiceTests.Document(1, "recent.pdf", isDeleted: true);
        recent.DeletedAtUtc = DateTime.UtcNow.AddDays(-29);
        var expired = PdfServiceTests.Document(1, "expired.pdf", isDeleted: true);
        expired.DeletedAtUtc = DateTime.UtcNow.AddDays(-30).AddMinutes(-1);
        var anotherUser = PdfServiceTests.Document(2, "other.pdf", isDeleted: true);
        test.Db.AddRange(recent, expired, anotherUser);
        await test.Db.SaveChangesAsync();
        var library = new PdfLibraryService(test.Repository, test.CurrentUser);

        var trash = await library.GetTrashAsync();

        var item = Assert.Single(trash.Pdfs);
        Assert.Equal("recent.pdf", item.FileName);
        Assert.InRange(item.DaysLeft, 1, 2);
    }

    [Fact]
    public void UploadEndpoint_EnforcesSizeAntiforgeryAndRateLimitContracts()
    {
        var controller = typeof(PdfDocumentsController);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<AutoValidateAntiforgeryTokenAttribute>());

        var upload = controller.GetMethod(nameof(PdfDocumentsController.UploadPdf));
        Assert.NotNull(upload);
        var requestSizeLimit = upload!.CustomAttributes.Single(
            attribute => attribute.AttributeType == typeof(RequestSizeLimitAttribute));
        Assert.Equal(
            PdfPolicy.MaxUploadRequestBytes,
            (long)requestSizeLimit.ConstructorArguments[0].Value!);
        Assert.Equal(
            PdfPolicy.MaxUploadRequestBytes,
            upload.GetCustomAttribute<RequestFormLimitsAttribute>()?
                .MultipartBodyLengthLimit);
        Assert.Equal(
            PdfRateLimitPolicies.Uploads,
            upload.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public void PdfPolicy_MatchesDocumentedUploadLimits()
    {
        Assert.Equal(25L * 1024 * 1024, PdfPolicy.MaxFileBytes);
        Assert.True(PdfPolicy.MaxUploadRequestBytes > PdfPolicy.MaxFileBytes);
        Assert.Equal(5_000, PdfPolicy.MaxPageCount);
    }
}
