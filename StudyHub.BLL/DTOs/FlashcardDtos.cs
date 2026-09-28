namespace StudyHub.BLL.DTOs;

public sealed record FlashcardDto(long Id, string FrontText, string BackText, int SortOrder);
public sealed record ReviewFlashcardRequestDto(Guid RequestId, long FlashcardId, byte Rating, int? ResponseMilliseconds);
public sealed record FlashcardScheduleDto(DateTime NextReviewAtUtc, int RepetitionCount, int IntervalDays, decimal EaseFactor);

public sealed record FlashcardDeckSummaryDto(
    long DeckId,
    string Title,
    string? Description,
    string Visibility,
    long? UserSubjectId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record FlashcardDeckDetailDto(
    long DeckId,
    long OwneruserId,
    long? UserSubjectId,
    long? StudyGroupId,
    string Title,
    string? Description,
    string Visibility,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateFlashcardDeckRequestDto(
    string? Title,
    string? Description,
    long? UserSubjectId
    );

public sealed record UpdateFlashcardDeckRequestDto(
    string? Title,
    string? Description,
    long? UserSubjectId
    );

//không sử dụng OwnerUserId cho Dto vì browser có thể gửi id rồi tạo deck cho người khác


public sealed record CreateFlashcardRequestDto(
    string? FrontText,
    string? BackText,
    int? SortOrder);

public sealed record UpdateFlashcardRequestDto(
    string? FrontText,
    string? BackText,
    int SortOrder);
