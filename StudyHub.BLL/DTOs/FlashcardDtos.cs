namespace StudyHub.BLL.DTOs;

public sealed record FlashcardDto(long Id, string FrontText, string BackText, int SortOrder);
public sealed record ReviewFlashcardRequestDto(Guid RequestId, long FlashcardId, byte Rating, int? ResponseMilliseconds);
public sealed record FlashcardScheduleDto(DateTime NextReviewAtUtc, int RepetitionCount, int IntervalDays, decimal EaseFactor);
