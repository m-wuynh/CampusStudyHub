using StudyHub.BLL.DTOs;
using StudyHub.DAL.Enums.Flashcards;

namespace StudyHub.BLL.Services.Flashcards;

public static class FlashcardScheduler
{
    public static FlashcardScheduleDto CalculateNextReview(
        FlashcardScheduleDto current,
        FlashcardReviewRating rating,
        DateTime reviewedAtUtc)
    {
        var easeFactor = current.EaseFactor;
        var repetitions = current.RepetitionCount;
        var interval = current.IntervalDays;

        if (rating == FlashcardReviewRating.Again)
        {
            repetitions = 0;
            interval = 1;
            easeFactor = Math.Max(1.30m, easeFactor - 0.20m);
        }
        else
        {
            repetitions++;
            interval = repetitions switch
            {
                1 => 1,
                2 => 6,
                _ => Math.Max(1, (int)Math.Round(interval * easeFactor))
            };

            easeFactor = rating switch
            {
                FlashcardReviewRating.Hard => Math.Max(1.30m, easeFactor - 0.15m),
                FlashcardReviewRating.Easy => easeFactor + 0.15m,
                _ => easeFactor
            };
        }

        return new FlashcardScheduleDto(
            reviewedAtUtc.AddDays(interval),
            repetitions,
            interval,
            decimal.Round(easeFactor, 2));
    }
}
