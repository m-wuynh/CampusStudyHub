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
            e.Description
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
            e.Description
        )).ToList();
    }

    public async Task<ScheduleEventDto> CreateEventAsync(long userId, CreateEventDto request)
    {
        var startLocal = request.Date.ToDateTime(request.StartTime);
        var endLocal = request.Date.ToDateTime(request.EndTime);

        if (endLocal <= startLocal)
        {
            endLocal = endLocal.AddDays(1);
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

        if (request.ReminderMinutes != null && request.ReminderMinutes.Any())
        {
            foreach (var min in request.ReminderMinutes)
            {
                var remindAt = startLocal.AddMinutes(-min).ToUniversalTime();
                var reminder = new Reminder
                {
                    UserId = userId,
                    ScheduleEventId = entity.ScheduleEventId,
                    OccurrenceDate = request.Date,
                    Channel = "InApp",
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
            entity.Description
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
            entity.Description);
    }

    public async Task UpdateEventAsync(long userId, UpdateEventDto request)
    {
        var startDateTime = request.Date.ToDateTime(request.StartTime);
        if (startDateTime < DateTime.Now)
            throw new Exception("Không thể đặt lịch trong quá khứ");

        var entity = await repository.FirstOrDefaultAsync<ScheduleEvent>(e => e.ScheduleEventId == request.Id && e.UserId == userId && !e.IsDeleted);
        if (entity == null) throw new Exception("Event not found");
        entity.Title = request.Title;
        entity.EventType = request.EventType;
        entity.StartAtLocal = request.Date.ToDateTime(request.StartTime);
        entity.EndAtLocal = request.Date.ToDateTime(request.EndTime);
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
        
        if (request.ReminderMinutes != null && request.ReminderMinutes.Any())
        {
            foreach (var min in request.ReminderMinutes)
            {
                var remindAt = startDateTime.AddMinutes(-min).ToUniversalTime();
                var reminder = new Reminder
                {
                    UserId = userId,
                    ScheduleEventId = entity.ScheduleEventId,
                    OccurrenceDate = request.Date,
                    Channel = "InApp",
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
}
