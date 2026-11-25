using StudyHub.DAL.Entities.Pdf;
using System.Linq.Expressions;

namespace StudyHub.BLL.Services.Pdf;

public static class PdfPolicy
{
    private const int LocalUtcOffsetHours = 7;

    public const long MaxFileBytes = 25L * 1024 * 1024;
    public const long MaxUploadRequestBytes = 27L * 1024 * 1024;
    public const int MaxPageCount = 5_000;
    public const int MaxDocumentsPerUser = 200;
    public const int MaxFileNameLength = PdfDocument.MaxFileNameLength;
    public const int MaxTitleLength = PdfDocument.MaxTitleLength;
    public const int MaxNoteLength = PdfDocumentPageNote.MaxContentLength;

    public const int TrashRetentionDays = 30;

    public const int MaxAiRequestsPerDay = 7;
    public const int MaxAiPagesPerRequest = 10;
    public const int MaxAiExtractedCharacters = 15_000;
    public const int MaxAiSummaryLength = 8_000;
    public const int MaxAiKeyPointCount = 12;
    public const int MaxAiKeyPointLength = 500;
    public const int MaxAiConceptCount = 12;
    public const int MaxAiConceptTitleLength = 200;
    public const int MaxAiConceptSummaryLength = 4_000;
    public const int MaxAiQuestionLength = 1_000;
    public const int MaxAiAnswerLength = 4_000;

    public static DateTime GetLocalDayStartUtc(DateTime utcNow)
    {
        var localDate = utcNow.ToUniversalTime()
            .AddHours(LocalUtcOffsetHours)
            .Date;

        return DateTime.SpecifyKind(
            localDate.AddHours(-LocalUtcOffsetHours),
            DateTimeKind.Utc);
    }

    public static Expression<Func<PdfAiAnalysis, bool>>
        AiRequestsUsedToday(long userId, DateTime utcNow)
    {
        var dayStart = GetLocalDayStartUtc(utcNow);

        return analysis =>
            analysis.OwnerUserId == userId &&
            analysis.CreatedAtUtc >= dayStart;
    }

    public static DateTime GetTrashExpiration(DateTime deletedAtUtc) =>
        deletedAtUtc.AddDays(TrashRetentionDays);

    public static DateTime GetTrashCutoff(DateTime utcNow) =>
        utcNow.AddDays(-TrashRetentionDays);
}
