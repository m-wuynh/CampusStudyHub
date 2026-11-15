using System.Text;
using StudyHub.BLL.Services.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class PdfValidationTests
{
    [Theory]
    [InlineData("document.pdf", "document.pdf")]
    [InlineData(" C:\\uploads\\document.PDF ", "document.PDF")]
    [InlineData("/tmp/report.pdf", "report.pdf")]
    public void PdfFileName_NormalizesAValidClientPath(
        string input,
        string expected) =>
        Assert.Equal(expected, PdfValidation.PdfFileName(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("report.txt")]
    [InlineData(".pdf")]
    [InlineData("bad\nname.pdf")]
    public void PdfFileName_RejectsInvalidNames(string? input)
    {
        var error = Assert.Throws<PdfException>(
            () => PdfValidation.PdfFileName(input));

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void PdfFileName_RejectsMoreThanTwoHundredCharacters()
    {
        var value = new string('a', 197) + ".pdf";

        var error = Assert.Throws<PdfException>(
            () => PdfValidation.PdfFileName(value));

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void Title_TrimsValidText()
    {
        Assert.Equal("Lecture notes", PdfValidation.Title("  Lecture notes  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\u0001title")]
    public void Title_RejectsInvalidText(string? input)
    {
        var error = Assert.Throws<PdfException>(() => PdfValidation.Title(input));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void Version_DecodesAValidToken()
    {
        var version = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        Assert.Equal(
            version,
            PdfValidation.Version(Convert.ToBase64String(version)));
    }

    [Fact]
    public void Version_RejectsMalformedBase64()
    {
        var error = Assert.Throws<PdfException>(
            () => PdfValidation.Version("not-base64"));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void PdfNoteText_NormalizesLineEndings()
    {
        Assert.Equal("first\nsecond", PdfValidation.PdfNoteText("first\r\nsecond"));
    }

    [Fact]
    public void PdfNoteText_UsesUtf8ByteLimit()
    {
        var oversized = string.Concat(Enumerable.Repeat("€", 34_134));
        Assert.True(Encoding.UTF8.GetByteCount(oversized) > 100 * 1024);

        var error = Assert.Throws<PdfException>(
            () => PdfValidation.PdfNoteText(oversized));

        Assert.Equal(413, error.StatusCode);
    }
}
