using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Calendar;

public interface ICalendarService
{
    Task<List<ScheduleEventDto>> GetEventsAsync(long userId, DateOnly fromDate, DateOnly toDate);
    Task<ScheduleEventDto?> GetEventAsync(long userId, long id);
    Task<ScheduleEventDto> CreateEventAsync(long userId, CreateEventDto request);
    Task UpdateEventAsync(long userId, UpdateEventDto request);
    Task DeleteEventAsync(long userId, long id);
    Task<List<ScheduleEventDto>> GetActiveRemindersAsync(long userId);
    Task<List<int>> GetReminderMinutesAsync(long userId, long eventId);
    Task ToggleEventCompletionAsync(long userId, long eventId, bool isCompleted);
}
