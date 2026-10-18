using StudyHub.BLL.DTOs;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudyHub.BLL.Services.Calendar;

public sealed class CalendarService(IRepository repository) : ICalendarService
{
    public async Task<List<ScheduleEventDto>> GetEventsAsync(long userId, DateOnly fromDate, DateOnly toDate)
    {
        var entities = await repository.ListAsync<ScheduleEvent>(e => e.UserId == userId && !e.IsDeleted);
        
        return entities.Select(e => new ScheduleEventDto(
            e.ScheduleEventId,
            e.Title,
            e.EventType,
            e.StartAtLocal,
            e.EndAtLocal,
            e.TimeZoneId,
            e.RepeatMode,
            e.RepeatUntilDate,
            e.Location,
            e.Description,
            e.GoalId,
            e.IsCompleted
        )).ToList();
    }

    public async Task<List<ScheduleEventDto>> GetActiveRemindersAsync(long userId)
    {
        var utcNow = DateTime.UtcNow;
        var reminders = await repository.ListAsync<Reminder>(r => r.UserId == userId && r.Status == "Pending" && r.RemindAtUtc <= utcNow);
        
        var eventIds = reminders.Select(r => r.ScheduleEventId).Distinct().Where(id => id.HasValue).Select(id => id!.Value).ToList();
        
        var events = await repository.ListAsync<ScheduleEvent>(e => eventIds.Contains(e.ScheduleEventId) && !e.IsDeleted);

        return events.Select(e => new ScheduleEventDto(
            e.ScheduleEventId,
            e.Title,
            e.EventType,
            e.StartAtLocal,
            e.EndAtLocal,
            e.TimeZoneId,
            e.RepeatMode,
            e.RepeatUntilDate,
            e.Location,
            e.Description,
            e.GoalId,
            e.IsCompleted
        )).ToList();
    }

    public async Task<List<ReminderInputDto>> GetEventRemindersAsync(long userId, long eventId)
    {
        var reminders = await repository.ListAsync<Reminder>(r => r.UserId == userId && r.ScheduleEventId == eventId);
        var result = new List<ReminderInputDto>();
        var ev = await repository.FirstOrDefaultAsync<ScheduleEvent>(e => e.ScheduleEventId == eventId);
        if (ev == null) return result;

        foreach (var r in reminders)
        {
            var diff = ev.StartAtLocal - r.RemindAtUtc.ToLocalTime();
            var minutes = (int)Math.Round(diff.TotalMinutes);
            if (minutes >= 0)
            {
                result.Add(new ReminderInputDto(minutes, r.Channel));
            }
        }
        return result;
    }

    public async Task<List<int>> GetReminderMinutesAsync(long userId, long eventId)
    {
        var list = await GetEventRemindersAsync(userId, eventId);
        return list.Select(r => r.Minutes).ToList();
    }

    public async Task<ScheduleEventDto> CreateEventAsync(long userId, CreateEventDto request)
    {
        var startLocal = request.Date.ToDateTime(request.StartTime);
        var endLocal = request.Date.ToDateTime(request.EndTime);

        if (endLocal <= startLocal)
        {
            throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");
        }

        var entity = new ScheduleEvent
        {
            UserId = userId,
            Title = request.Title,
            EventType = request.EventType,
            StartAtLocal = startLocal,
            EndAtLocal = endLocal,
            TimeZoneId = "SE Asia Standard Time",
            RepeatMode = "None",
            Location = request.Location,
            Description = request.Description,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        await repository.AddAsync(entity);
        await repository.SaveChangesAsync();

        if (request.Reminders != null && request.Reminders.Any())
        {
            var distinctReminders = request.Reminders
                .Where(rem => rem.Minutes >= 0)
                .Select(rem =>
                {
                    var isEmail = string.Equals(rem.Channel, "email", StringComparison.OrdinalIgnoreCase) 
                               || string.Equals(rem.Channel, "gmail", StringComparison.OrdinalIgnoreCase);
                    return new ReminderInputDto(rem.Minutes, isEmail ? "Email" : "InApp");
                })
                .GroupBy(rem => new { rem.Minutes, rem.Channel })
                .Select(g => g.First())
                .ToList();

            foreach (var rem in distinctReminders)
            {
                var remindAt = startLocal.AddMinutes(-rem.Minutes).ToUniversalTime();
                var reminder = new Reminder
                {
                    UserId = userId,
                    ScheduleEventId = entity.ScheduleEventId,
                    OccurrenceDate = request.Date,
                    Channel = rem.Channel,
                    RemindAtUtc = remindAt,
                    Status = "Pending",
                    SentAtUtc = null
                };
                await repository.AddAsync(reminder);
            }
            await repository.SaveChangesAsync();
        }

        return new ScheduleEventDto(
            entity.ScheduleEventId,
            entity.Title,
            entity.EventType,
            entity.StartAtLocal,
            entity.EndAtLocal,
            entity.TimeZoneId,
            entity.RepeatMode,
            entity.RepeatUntilDate,
            entity.Location,
            entity.Description,
            entity.GoalId,
            entity.IsCompleted
        );
    }

    public async Task<ScheduleEventDto?> GetEventAsync(long userId, long id)
    {
        var entity = await repository.FirstOrDefaultAsync<ScheduleEvent>(e => e.ScheduleEventId == id && e.UserId == userId && !e.IsDeleted);
        if (entity == null) return null;
        return new ScheduleEventDto(
            entity.ScheduleEventId,
            entity.Title,
            entity.EventType,
            entity.StartAtLocal,
            entity.EndAtLocal,
            entity.TimeZoneId,
            entity.RepeatMode,
            entity.RepeatUntilDate,
            entity.Location,
            entity.Description,
            entity.GoalId,
            entity.IsCompleted);
    }

    public async Task UpdateEventAsync(long userId, UpdateEventDto request)
    {
        var startDateTime = request.Date.ToDateTime(request.StartTime);
        var endDateTime = request.Date.ToDateTime(request.EndTime);

        if (endDateTime <= startDateTime)
        {
            throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");
        }

        var entity = await repository.FirstOrDefaultAsync<ScheduleEvent>(e => e.ScheduleEventId == request.Id && e.UserId == userId && !e.IsDeleted);
        if (entity == null) throw new KeyNotFoundException("Sự kiện không tồn tại.");
        entity.Title = request.Title;
        entity.EventType = request.EventType;
        entity.StartAtLocal = startDateTime;
        entity.EndAtLocal = endDateTime;
        entity.Location = request.Location;
        entity.Description = request.Description;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        repository.Update(entity);

        // Optional: clear existing reminders and add new ones
        var existingReminders = await repository.ListAsync<Reminder>(r => r.ScheduleEventId == entity.ScheduleEventId && r.UserId == userId);
        if (existingReminders.Any())
        {
            repository.RemoveRange(existingReminders);
        }
        
        if (request.Reminders != null && request.Reminders.Any())
        {
            var distinctReminders = request.Reminders
                .Where(rem => rem.Minutes >= 0)
                .Select(rem =>
                {
                    var isEmail = string.Equals(rem.Channel, "email", StringComparison.OrdinalIgnoreCase) 
                               || string.Equals(rem.Channel, "gmail", StringComparison.OrdinalIgnoreCase);
                    return new ReminderInputDto(rem.Minutes, isEmail ? "Email" : "InApp");
                })
                .GroupBy(rem => new { rem.Minutes, rem.Channel })
                .Select(g => g.First())
                .ToList();

            foreach (var rem in distinctReminders)
            {
                var remindAt = startDateTime.AddMinutes(-rem.Minutes).ToUniversalTime();
                var reminder = new Reminder
                {
                    UserId = userId,
                    ScheduleEventId = entity.ScheduleEventId,
                    OccurrenceDate = request.Date,
                    Channel = rem.Channel,
                    RemindAtUtc = remindAt,
                    Status = "Pending"
                };
                await repository.AddAsync(reminder);
            }
        }
        await repository.SaveChangesAsync();
    }

    public async Task DeleteEventAsync(long userId, long id)
    {
        var entity = await repository.FirstOrDefaultAsync<ScheduleEvent>(e => e.ScheduleEventId == id && e.UserId == userId && !e.IsDeleted);
        if (entity == null) throw new Exception("Event not found");
        
        entity.IsDeleted = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        repository.Update(entity);
        await repository.SaveChangesAsync();
    }

    public async Task ToggleEventCompletionAsync(long userId, long eventId, bool isCompleted)
    {
        var entity = await repository.FirstOrDefaultAsync<ScheduleEvent>(e => e.ScheduleEventId == eventId && e.UserId == userId && !e.IsDeleted);
        if (entity == null) throw new KeyNotFoundException("Sự kiện không tồn tại.");

        if (isCompleted && entity.EndAtLocal < DateTime.Now)
        {
            throw new InvalidOperationException("Ca học đã quá hạn và được tính là Bỏ lỡ (Missed), không thể cập nhật.");
        }

        entity.IsCompleted = isCompleted;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        repository.Update(entity);
        await repository.SaveChangesAsync();
    }

    public async Task<bool> ToggleEventCompletionAsync(long userId, long id)
    {
        var entity = await repository.FirstOrDefaultAsync<ScheduleEvent>(e => e.ScheduleEventId == id && e.UserId == userId && !e.IsDeleted);
        if (entity == null) throw new KeyNotFoundException("Sự kiện không tồn tại.");

        if (!entity.IsCompleted && entity.EndAtLocal < DateTime.Now)
        {
            throw new InvalidOperationException("Ca học đã quá hạn và được tính là Bỏ lỡ (Missed), không thể cập nhật.");
        }

        entity.IsCompleted = !entity.IsCompleted;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        repository.Update(entity);
        await repository.SaveChangesAsync();
        return entity.IsCompleted;
    }
}
