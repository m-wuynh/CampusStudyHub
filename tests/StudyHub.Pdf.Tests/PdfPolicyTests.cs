using StudyHub.BLL.Services.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class PdfPolicyTests
{
    [Theory]
    [InlineData("2026-11-25T16:59:59Z", "2026-11-24T17:00:00Z")]
    [InlineData("2026-11-25T17:00:00Z", "2026-11-25T17:00:00Z")]
    public void GetLocalDayStartUtc_ResetsAtMidnightVietnam(
        string utcNow,
        string expected)
    {
        var result = PdfPolicy.GetLocalDayStartUtc(DateTime.Parse(utcNow));

        Assert.Equal(DateTime.Parse(expected).ToUniversalTime(), result);
    }
}
