using StudyHub.BLL.DTOs.Flashcards;
using StudyHub.BLL.Services.Flashcards;
using StudyHub.DAL.Enums.Flashcards;

namespace StudyHub.Flashcards.Tests;

public sealed class FlashcardSchedulerTests
{
    [Theory]
    [InlineData(FlashcardReviewRating.Again, 5, 12, "2.50", 0, 1, "2.30")]
    [InlineData(FlashcardReviewRating.Again, 1, 6, "1.35", 0, 1, "1.30")]
    [InlineData(FlashcardReviewRating.Hard, 0, 0, "2.50", 1, 1, "2.35")]
    [InlineData(FlashcardReviewRating.Hard, 1, 1, "1.35", 2, 6, "1.30")]
    [InlineData(FlashcardReviewRating.Good, 1, 1, "2.50", 2, 6, "2.50")]
    [InlineData(FlashcardReviewRating.Good, 2, 5, "2.50", 3, 12, "2.50")]
    [InlineData(FlashcardReviewRating.Good, 2, 0, "2.50", 3, 1, "2.50")]
    [InlineData(FlashcardReviewRating.Good, 2, 6, "2.555", 3, 15, "2.56")]
    [InlineData(FlashcardReviewRating.Easy, 2, 6, "2.40", 3, 14, "2.55")]
    public void CalculateNextReview_AppliesRatingAndRepetitionRules(
        FlashcardReviewRating rating,
        int repetitions,
        int interval,
        string ease,
        int expectedRepetitions,
        int expectedInterval,
        string expectedEase)
    {
        var reviewedAt = new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Utc);
        var current = new FlashcardScheduleDto(
            reviewedAt.AddDays(-3), repetitions, interval, ParseEase(ease));

        var next = FlashcardScheduler.CalculateNextReview(current, rating, reviewedAt);

        Assert.Equal(new FlashcardScheduleDto(
            reviewedAt.AddDays(expectedInterval),
            expectedRepetitions,
            expectedInterval,
            ParseEase(expectedEase)), next);
        Assert.Equal(DateTimeKind.Utc, next.NextReviewAtUtc.Kind);
    }

    private static decimal ParseEase(string value) =>
        decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
