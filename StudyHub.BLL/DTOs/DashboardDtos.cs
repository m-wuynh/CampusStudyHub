namespace StudyHub.BLL.DTOs;

public sealed record StudyStreakDto(long UserId, DateOnly? LastStudyDate, int CurrentStreakDays, int LongestStreakDays);
public sealed record DashboardSummaryDto(decimal? CurrentAverage10, int CurrentStreakDays, int PendingDeadlineCount, int DueFlashcardCount);
