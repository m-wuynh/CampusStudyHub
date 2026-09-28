using StudyHub.BLL.DTOs;
using StudyHub.DAL.Enums.Calendar;

namespace StudyHub.BLL.Services.Calendar;

public static class ScheduleOccurrenceService
{
    public static IReadOnlyList<DateTime> GetStarts(
        ScheduleEventDto schedule,
        DateOnly fromDate,
        DateOnly toDate)
    {
        if (toDate < fromDate) return [];

        var startDate = DateOnly.FromDateTime(schedule.StartAtLocal);
        var lastDate = schedule.RepeatUntilDate ?? startDate;
        var rangeStart = fromDate > startDate ? fromDate : startDate;
        var rangeEnd = toDate < lastDate ? toDate : lastDate;
        if (rangeEnd < rangeStart) return [];

        if (!Enum.TryParse<ScheduleRepeatMode>(schedule.RepeatMode, true, out var repeatMode)
            || repeatMode == ScheduleRepeatMode.None)
        {
            return startDate >= rangeStart && startDate <= rangeEnd
                ? [schedule.StartAtLocal]
                : [];
        }

        var result = new List<DateTime>();
        var offset = ((int)startDate.DayOfWeek - (int)rangeStart.DayOfWeek + 7) % 7;
        var occurrenceDate = rangeStart.AddDays(offset);
        var startTime = TimeOnly.FromDateTime(schedule.StartAtLocal);

        while (occurrenceDate <= rangeEnd)
        {
            result.Add(occurrenceDate.ToDateTime(startTime));
            occurrenceDate = occurrenceDate.AddDays(7);
        }

        return result;
    }
}
