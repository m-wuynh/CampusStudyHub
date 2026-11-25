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
        
        return entities
            .Select(e => new ScheduleEventDto(
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
            ))
            .Where(dto => ScheduleOccurrenceService.GetStarts(dto, fromDate, toDate).Count > 0)
            .ToList();
    }

    public async Task<List<ScheduleEventDto>> GetActiveRemindersAsync(long userId)
    {
        var utcNow = DateTime.UtcNow;
        // Chỉ lấy nhắc nhở dạng thông báo thường trên web (InApp), loại trừ thông báo Gmail/Email
        var minRemindAtUtc = utcNow.AddHours(-24);
        var reminders = await repository.ListAsync<Reminder>(r => 
            r.UserId == userId && 
            r.Status == "Pending" && 
            r.RemindAtUtc <= utcNow && 
            r.RemindAtUtc >= minRemindAtUtc &&
            r.Channel != "Email" && 
            r.Channel != "gmail");
        
        var eventIds = reminders.Select(r => r.ScheduleEventId).Distinct().Where(id => id.HasValue).Select(id => id!.Value).ToList();
        
        var now = DateTime.Now;
        var events = await repository.ListAsync<ScheduleEvent>(e => eventIds.Contains(e.ScheduleEventId) && !e.IsDeleted && e.EndAtLocal >= now);

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

    private static string NormalizeDbEventType(string? type)
    {
        var t = type?.Trim();
        return string.IsNullOrWhiteSpace(t) ? "Tự học" : t;
    }

    public async Task<ScheduleEventDto> CreateEventAsync(long userId, CreateEventDto request)
    {
        var startLocal = request.Date.ToDateTime(request.StartTime);
        var endLocal = request.Date.ToDateTime(request.EndTime);

        if (endLocal <= startLocal)
        {
            throw new ArgumentException("Giờ không hợp lệ , Vui lòng đặt lại");
        }

        var entity = new ScheduleEvent
        {
            UserId = userId,
            Title = request.Title,
            EventType = NormalizeDbEventType(request.EventType),
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
        entity.EventType = NormalizeDbEventType(request.EventType);
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

        // Hủy toàn bộ nhắc nhở đang chờ (Pending) của sự kiện vừa bị xóa
        var pendingReminders = await repository.ListAsync<Reminder>(r => r.ScheduleEventId == id && r.Status == "Pending");
        foreach (var rem in pendingReminders)
        {
            rem.Status = "Cancelled";
            repository.Update(rem);
        }

        await repository.SaveChangesAsync();
    }

    public async Task ToggleEventCompletionAsync(long userId, long eventId, bool isCompleted)
    {
        var entity = await repository.FirstOrDefaultAsync<ScheduleEvent>(e => e.ScheduleEventId == eventId && e.UserId == userId && !e.IsDeleted);
        if (entity == null) throw new KeyNotFoundException("Sự kiện không tồn tại.");

        if (entity.EndAtLocal < DateTime.Now)
        {
            throw new InvalidOperationException("Ca học đã kết thúc, không thể thay đổi trạng thái.");
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

        if (entity.EndAtLocal < DateTime.Now)
        {
            throw new InvalidOperationException("Ca học đã kết thúc, không thể thay đổi trạng thái.");
        }

        entity.IsCompleted = !entity.IsCompleted;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        repository.Update(entity);
        await repository.SaveChangesAsync();
        return entity.IsCompleted;
    }

    public async Task<List<ScheduleEventDto>> SearchEventsAsync(long userId, string keyword, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        var kw = keyword.Trim();

        var entities = await repository.ListAsync<ScheduleEvent>(e =>
            e.UserId == userId &&
            !e.IsDeleted &&
            (e.Title.Contains(kw) ||
             (e.Location != null && e.Location.Contains(kw)) ||
             (e.Description != null && e.Description.Contains(kw))));

        var now = DateTime.Now;

        return entities
            .OrderBy(e => e.EndAtLocal < now) // Upcoming or happening events first
            .ThenBy(e => Math.Abs((e.StartAtLocal - now).TotalMinutes)) // Closest to current time
            .Take(maxResults)
            .Select(e => new ScheduleEventDto(
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

    public async Task<List<CalendarCategoryDto>> GetCategoriesAsync(long userId)
    {
        var categories = await repository.ListAsync<CalendarCategory>(c => c.UserId == userId);

        if (categories.Count == 0)
        {
            var defaults = new List<CalendarCategory>
            {
                new() { UserId = userId, Name = "Tự học", Description = "Các ca tự học cá nhân, ôn bài tại nhà", IsDefault = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
                new() { UserId = userId, Name = "Bài tập / Nộp bài", Description = "Làm bài tập về nhà, nộp báo cáo, bài tập nhóm", IsDefault = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
                new() { UserId = userId, Name = "Kiểm tra / Thi", Description = "Các bài kiểm tra định kỳ 15p, 1 tiết, thi học kỳ", IsDefault = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
                new() { UserId = userId, Name = "Học nhóm", Description = "Các buổi học cùng bạn bè, học nhóm trên lớp", IsDefault = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }
            };

            await repository.AddRangeAsync(defaults);
            await repository.SaveChangesAsync();
            categories = defaults;
        }

        var events = await repository.ListAsync<ScheduleEvent>(e => e.UserId == userId && !e.IsDeleted);

        return categories
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.Name)
            .Select(c =>
            {
                var count = events.Count(e =>
                    string.Equals(e.EventType, c.Name, StringComparison.OrdinalIgnoreCase));

                return new CalendarCategoryDto(c.CategoryId, c.Name, c.Description, c.IsDefault, count);
            })
            .ToList();
    }

    public async Task<CalendarCategoryDto> CreateCategoryAsync(long userId, SaveCalendarCategoryDto request)
    {
        var name = (request.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên loại sự kiện không được để trống.");

        var existing = await repository.FirstOrDefaultAsync<CalendarCategory>(c =>
            c.UserId == userId && c.Name.ToLower() == name.ToLower());

        if (existing != null)
            throw new InvalidOperationException("Loại sự kiện này đã tồn tại.");

        var entity = new CalendarCategory
        {
            UserId = userId,
            Name = name,
            Description = request.Description?.Trim(),
            IsDefault = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(entity);
        await repository.SaveChangesAsync();

        return new CalendarCategoryDto(entity.CategoryId, entity.Name, entity.Description, entity.IsDefault, 0);
    }

    public async Task UpdateCategoryAsync(long userId, long categoryId, SaveCalendarCategoryDto request)
    {
        var name = (request.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên loại sự kiện không được để trống.");

        var category = await repository.FirstOrDefaultAsync<CalendarCategory>(c =>
            c.CategoryId == categoryId && c.UserId == userId);

        if (category == null)
            throw new KeyNotFoundException("Không tìm thấy loại sự kiện cần chỉnh sửa.");

        var duplicate = await repository.FirstOrDefaultAsync<CalendarCategory>(c =>
            c.UserId == userId && c.CategoryId != categoryId && c.Name.ToLower() == name.ToLower());

        if (duplicate != null)
            throw new InvalidOperationException("Tên loại sự kiện đã được sử dụng.");

        var oldName = category.Name;
        category.Name = name;
        category.Description = request.Description?.Trim();
        category.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(category);

        if (!string.Equals(oldName, name, StringComparison.OrdinalIgnoreCase))
        {
            var affectedEvents = await repository.ListAsync<ScheduleEvent>(e =>
                e.UserId == userId && !e.IsDeleted && e.EventType == oldName);

            foreach (var ev in affectedEvents)
            {
                ev.EventType = NormalizeDbEventType(name);
                ev.UpdatedAtUtc = DateTime.UtcNow;
                repository.Update(ev);
            }
        }

        await repository.SaveChangesAsync();
    }

    public async Task DeleteCategoryAsync(long userId, long categoryId)
    {
        var category = await repository.FirstOrDefaultAsync<CalendarCategory>(c =>
            c.CategoryId == categoryId && c.UserId == userId);

        if (category == null)
            throw new KeyNotFoundException("Không tìm thấy loại sự kiện cần xóa.");

        var totalCategories = await repository.CountAsync<CalendarCategory>(c => c.UserId == userId);
        if (totalCategories <= 1)
            throw new InvalidOperationException("Hệ thống phải có ít nhất một loại sự kiện, không thể xóa loại cuối cùng.");

        var fallbackCategory = await repository.FirstOrDefaultAsync<CalendarCategory>(c =>
            c.UserId == userId && c.CategoryId != categoryId);
        var fallbackName = fallbackCategory?.Name ?? "Tự học";

        var catName = category.Name;
        var catNameLower = catName.ToLower();
        var affectedEvents = await repository.ListAsync<ScheduleEvent>(e =>
            e.UserId == userId && !e.IsDeleted && e.EventType.ToLower() == catNameLower);

        foreach (var ev in affectedEvents)
        {
            ev.EventType = NormalizeDbEventType(fallbackName);
            ev.UpdatedAtUtc = DateTime.UtcNow;
            repository.Update(ev);
        }

        repository.Remove(category);
        await repository.SaveChangesAsync();
    }
}

