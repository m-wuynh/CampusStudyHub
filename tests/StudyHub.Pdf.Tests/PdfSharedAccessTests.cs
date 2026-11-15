using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Access;
using StudyHub.BLL.Services.Pdf.Annotations;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.BLL.Services.Pdf.Notes;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Entities.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class PdfSharedAccessTests
{
    [Fact]
    public async Task ActiveGroupMember_CanReadSharedOriginalWithoutPrivateData()
    {
        await using var test = new PdfTestContext(userId: 2);
        var seeded = await SeedSharedPdfAsync(test, memberStatus: "Active");
        test.Db.PdfDocumentPageNotes.Add(new PdfDocumentPageNote
        {
            DocumentId = seeded.Document.DocumentId,
            OwnerUserId = 1,
            PhysicalPageNumber = 1,
            ContentText = "owner private note",
            RowVersion = [4]
        });
        test.Db.PdfAnnotations.Add(new PdfAnnotation
        {
            DocumentId = seeded.Document.DocumentId,
            OwnerUserId = 1,
            PhysicalPageNumber = 1,
            PayloadJson = "{\"kind\":\"Highlight\",\"color\":\"#ffff00\",\"x\":0.1,\"y\":0.1,\"width\":0.2,\"height\":0.1}",
            RowVersion = [5]
        });
        await test.Db.SaveChangesAsync();
        var services = CreateServices(test);

        var details = await services.Pdfs.GetAsync(
            seeded.Document.DocumentId,
            seeded.Share.PdfDocumentShareId);
        var note = await services.Notes.GetTextAsync(
            seeded.Document.DocumentId,
            1,
            seeded.Share.PdfDocumentShareId);
        var marks = await services.Marks.ListAsync(
            seeded.Document.DocumentId,
            1,
            seeded.Share.PdfDocumentShareId);

        Assert.Equal(10, details.GroupId);
        Assert.Equal("shared.pdf", details.FileName);
        Assert.Equal(string.Empty, note.Text);
        Assert.Null(note.VersionToken);
        Assert.Empty(marks);
    }

    [Fact]
    public async Task SharedPdf_IsReadOnlyForNotes()
    {
        await using var test = new PdfTestContext(userId: 2);
        var seeded = await SeedSharedPdfAsync(test, memberStatus: "Active");
        var services = CreateServices(test);

        var error = await Assert.ThrowsAsync<PdfException>(() =>
            services.Notes.SaveTextAsync(
                seeded.Document.DocumentId,
                1,
                "must not be saved",
                null,
                seeded.Share.PdfDocumentShareId));

        Assert.Equal(403, error.StatusCode);
        Assert.Empty(await test.Db.PdfDocumentPageNotes.ToListAsync());
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Banned")]
    public async Task NonActiveMember_CannotOpenSharedPdf(string status)
    {
        await using var test = new PdfTestContext(userId: 2);
        var seeded = await SeedSharedPdfAsync(test, memberStatus: status);
        var services = CreateServices(test);

        var error = await Assert.ThrowsAsync<PdfException>(() =>
            services.Pdfs.GetAsync(
                seeded.Document.DocumentId,
                seeded.Share.PdfDocumentShareId));

        Assert.Equal(403, error.StatusCode);
    }

    private static ServiceSet CreateServices(PdfTestContext test)
    {
        var access = new PdfAccessService(test.Repository, test.CurrentUser);
        return new ServiceSet(
            new PdfService(
                test.Repository,
                test.CurrentUser,
                access,
                new FakePdfStorage(),
                NullLogger<PdfService>.Instance),
            new PdfPageNotesService(test.Repository, test.CurrentUser, access),
            new PdfMarksService(test.Repository, test.CurrentUser, access));
    }

    private static async Task<(PdfDocument Document, PdfDocumentShare Share)>
        SeedSharedPdfAsync(PdfTestContext test, string memberStatus)
    {
        var document = PdfServiceTests.Document(1, "shared.pdf");
        var group = new StudyGroup
        {
            StudyGroupId = 10,
            OwnerUserId = 1,
            GroupName = "Study group",
            MeetingFormat = "Online",
            Visibility = "Private",
            JoinMode = "Approval",
            IsArchived = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            RowVersion = [1]
        };
        test.Db.AddRange(document, group);
        test.Db.Add(new GroupMember
        {
            StudyGroupId = 10,
            UserId = 2,
            MemberRole = "Member",
            Status = memberStatus,
            RequestedAtUtc = DateTime.UtcNow
        });
        await test.Db.SaveChangesAsync();

        var share = new PdfDocumentShare
        {
            DocumentId = document.DocumentId,
            StudyGroupId = 10,
            SharedByUserId = 1,
            IsActive = true,
            SharedAtUtc = DateTime.UtcNow,
            RowVersion = [2]
        };
        test.Db.Add(share);
        await test.Db.SaveChangesAsync();
        return (document, share);
    }

    private sealed record ServiceSet(
        PdfService Pdfs,
        PdfPageNotesService Notes,
        PdfMarksService Marks);
}
