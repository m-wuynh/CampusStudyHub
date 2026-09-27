namespace StudyHub.BLL.DTOs;

public sealed record ScheduleEventDto(
    long Id,
    string Title,
    string EventType,
    DateTime StartAtLocal,
    DateTime EndAtLocal,
    string TimeZoneId,
    string RepeatMode,
    DateOnly? RepeatUntilDate,
    string? Location,
    string? Description);

public sealed record DeadlineDto(long Id, string Title, DateTime DueAtUtc, byte Priority, string Status);

public sealed record CreateEventDto(
    string Title,
    string EventType,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Location,
    string? Description);
public sealed class UpdateEventDto
{
    public long Id { get; set; }
    public string Title { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
}
