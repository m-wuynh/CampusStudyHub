namespace StudyHub.BLL.DTOs;

public sealed record ScheduleEventDto(
    long Id,
    string Title,
    string EventType,
    DateTime StartAtLocal,
    DateTime EndAtLocal,
    string TimeZoneId,
    string RepeatMode,
    DateOnly? RepeatUntilDate);

public sealed record DeadlineDto(long Id, string Title, DateTime DueAtUtc, byte Priority, string Status);
