using StudyHub.BLL.DTOs;

namespace StudyHub.Web.ViewModels.Calendar;

public sealed record CalendarUpdateViewModel(
    long Id,
    string Title,
    string EventType,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Location,
    string? Description,
    List<int>? ReminderMinutes = null);
