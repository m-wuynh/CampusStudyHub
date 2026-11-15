using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.Web.Services.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class LocalPdfFileStorageTests : IDisposable
{
    private readonly string root = Path.Combine(
        AppContext.BaseDirectory,
        $"studyhub-pdf-storage-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndOpen_PreservesAValidPdf()
    {
        var storage = CreateStorage();
        var source = PdfTestData.OnePagePdf();

        var stored = await storage.SaveAsync(
            new MemoryStream(source),
            "lecture.pdf");

        Assert.Equal("lecture.pdf", stored.FileName);
        Assert.Equal(source.Length, stored.SizeBytes);
        Assert.Matches("^[0-9a-f]{32}\\.pdf$", stored.StorageKey);

        await using var opened = await storage.OpenReadAsync(stored.StorageKey);
        using var copy = new MemoryStream();
        await opened.CopyToAsync(copy);
        Assert.Equal(source, copy.ToArray());
    }

    [Fact]
    public async Task Save_RejectsRenamedNonPdfAndLeavesNoFile()
    {
        var storage = CreateStorage();
        var source = new MemoryStream("plain text"u8.ToArray());

        var error = await Assert.ThrowsAsync<PdfException>(
            () => storage.SaveAsync(source, "fake.pdf"));

        Assert.Equal(400, error.StatusCode);
        Assert.Empty(AllStoredFiles());
    }

    [Fact]
    public async Task Save_RejectsMoreThanTwentyFiveMiBAndCleansTemporaryFile()
    {
        var storage = CreateStorage();
        await using var source = new SizedPdfStream(PdfPolicy.MaxFileBytes + 1);

        var error = await Assert.ThrowsAsync<PdfException>(
            () => storage.SaveAsync(source, "large.pdf"));

        Assert.Equal(413, error.StatusCode);
        Assert.Empty(AllStoredFiles());
    }

    [Theory]
    [InlineData("../outside.pdf")]
    [InlineData("document.pdf")]
    [InlineData("")]
    public async Task Open_RejectsKeysNotCreatedByStorage(string key)
    {
        var storage = CreateStorage();

        var error = await Assert.ThrowsAsync<PdfException>(
            () => storage.OpenReadAsync(key));

        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    public async Task DeleteOrphans_DeletesOnlyOldUnreferencedFiles()
    {
        var storage = CreateStorage();
        var first = await storage.SaveAsync(
            new MemoryStream(PdfTestData.OnePagePdf()), "keep.pdf");
        var second = await storage.SaveAsync(
            new MemoryStream(PdfTestData.OnePagePdf()), "remove.pdf");
        var pdfRoot = Path.Combine(root, "App_Data", "Pdf");
        File.SetLastWriteTimeUtc(
            Path.Combine(pdfRoot, second.StorageKey),
            DateTime.UtcNow.AddDays(-2));

        var removed = await storage.DeleteOrphansAsync(
            [first.StorageKey],
            DateTime.UtcNow.AddDays(-1));

        Assert.Equal(1, removed);
        Assert.True(File.Exists(Path.Combine(pdfRoot, first.StorageKey)));
        Assert.False(File.Exists(Path.Combine(pdfRoot, second.StorageKey)));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private LocalPdfFileStorage CreateStorage()
    {
        Directory.CreateDirectory(root);
        return new LocalPdfFileStorage(new TestWebHostEnvironment(root));
    }

    private string[] AllStoredFiles()
    {
        var path = Path.Combine(root, "App_Data", "Pdf");
        return Directory.Exists(path)
            ? Directory.GetFiles(path)
            : [];
    }

    private sealed class TestWebHostEnvironment(string contentRoot)
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "StudyHub.Pdf.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(contentRoot, "wwwroot");
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(contentRoot);
    }

    private sealed class SizedPdfStream(long length) : Stream
    {
        private static readonly byte[] Header = "%PDF-"u8.ToArray();
        private long position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var remaining = length - position;
            if (remaining <= 0) return 0;

            var count = (int)Math.Min(buffer.Length, remaining);
            buffer[..count].Clear();
            for (var index = 0; index < count; index++)
            {
                var sourceIndex = position + index;
                if (sourceIndex < Header.Length)
                {
                    buffer[index] = Header[sourceIndex];
                }
            }

            position += count;
            return count;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
