using System.ComponentModel.DataAnnotations;

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
    string? Description,
    long? GoalId = null,
    bool IsCompleted = false);

public sealed record DeadlineDto(long Id, string Title, DateTime DueAtUtc, byte Priority, string Status);
public sealed record ReminderInputDto(int Minutes, string Channel);

public sealed record CreateEventDto(
    string Title,
    string EventType,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Location,
    string? Description, List<ReminderInputDto>? Reminders);
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
    public List<ReminderInputDto>? Reminders { get; set; }
}

public sealed record CalendarCategoryDto(long Id, string Name, string? Description, bool IsDefault, int EventCount);

public sealed record SaveCalendarCategoryDto(string Name, string? Description = null);

public sealed record WeeklyProgressDto(
    int TotalEvents,
    int CompletedEvents,
    int MissedEvents,
    int UpcomingEvents,
    int CompletionPct
);

public sealed class CategoryStatItemDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public double PercentageOfTotal { get; set; }
    public int CompletedEvents { get; set; }
    public int CompletionPct { get; set; }
    public int MissedEvents { get; set; }
    public string StatusBadge { get; set; } = "Bình thường";
    public string StatusBadgeType { get; set; } = "primary";
}

public class CalendarReportsDto
{
    public string Range { get; set; } = "this-week";
    public string RangeLabel { get; set; } = "Tuần này";
    public string DateRangeText { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public double AvgPerDay { get; set; }
    public int CompletedEvents { get; set; }
    public int CompletionPct { get; set; }
    public int MissedEvents { get; set; }
    public int UpcomingEvents { get; set; }
    public List<CategoryStatItemDto> CategoryStats { get; set; } = new();
    public string InsightTitle { get; set; } = string.Empty;
    public string InsightMessage { get; set; } = string.Empty;
    public string InsightBadge { get; set; } = string.Empty;
    public string InsightBadgeType { get; set; } = "success";
    public string InsightRecommendation { get; set; } = string.Empty;
}
