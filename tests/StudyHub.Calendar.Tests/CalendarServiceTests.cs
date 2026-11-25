using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.Calendar;
using StudyHub.DAL.Entities;
using Xunit;

namespace StudyHub.Calendar.Tests;

public sealed class CalendarServiceTests
{
    [Fact]
    public async Task CreateEventAsync_ShouldCreateSuccessfully_WithNormalizedTypeAndReminders()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarService(context.Repository);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var dto = new CreateEventDto(
            Title: "Học lập trình C#",
            EventType: "   ", // Trống để kiểm tra chuẩn hóa thành "Tự học"
            Date: today,
            StartTime: new TimeOnly(8, 0),
            EndTime: new TimeOnly(10, 0),
            Location: "Thư viện tầng 2",
            Description: "Ôn tập kiến thức LINQ & EF Core",
            Reminders: new List<ReminderInputDto>
            {
                new(15, "InApp"),
                new(30, "Email")
            }
        );

        // Act
        var result = await service.CreateEventAsync(context.UserId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Học lập trình C#", result.Title);
        Assert.Equal("Tự học", result.EventType); // Đã chuẩn hóa
        Assert.Equal("Thư viện tầng 2", result.Location);
        Assert.False(result.IsCompleted);

        // Kiểm tra database
        var savedEvent = await context.Db.ScheduleEvents.FirstOrDefaultAsync(e => e.ScheduleEventId == result.Id);
        Assert.NotNull(savedEvent);
        Assert.Equal(context.UserId, savedEvent.UserId);
        Assert.False(savedEvent.IsDeleted);

        // Kiểm tra nhắc nhở đã được tạo
        var savedReminders = await context.Db.Reminders.Where(r => r.ScheduleEventId == result.Id).ToListAsync();
        Assert.Equal(2, savedReminders.Count);
        Assert.Contains(savedReminders, r => r.Channel == "InApp");
        Assert.Contains(savedReminders, r => r.Channel == "Email");
    }

    [Fact]
    public async Task CreateEventAsync_ShouldThrowArgumentException_WhenEndTimeIsBeforeOrEqualStartTime()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarService(context.Repository);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var invalidDto = new CreateEventDto(
            Title: "Lỗi thời gian",
            EventType: "Tự học",
            Date: today,
            StartTime: new TimeOnly(11, 0),
            EndTime: new TimeOnly(10, 0), // EndTime < StartTime
            Location: null,
            Description: null,
            Reminders: null
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateEventAsync(context.UserId, invalidDto));

        Assert.Contains("Giờ không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task ToggleEventCompletionAsync_ShouldUpdateStatus_WhenEventIsInFuture()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarService(context.Repository);

        var futureEvent = CalendarTestContext.CreateEvent(
            context.UserId,
            "Ca học sắp tới",
            "Học nhóm",
            DateTime.Now.AddHours(1),
            DateTime.Now.AddHours(3)
        );
        await context.Db.ScheduleEvents.AddAsync(futureEvent);
        await context.Db.SaveChangesAsync();

        // Act
        var newStatus = await service.ToggleEventCompletionAsync(context.UserId, futureEvent.ScheduleEventId);

        // Assert
        Assert.True(newStatus);
        var updated = await context.Db.ScheduleEvents.FindAsync(futureEvent.ScheduleEventId);
        Assert.NotNull(updated);
        Assert.True(updated.IsCompleted);
    }

    [Fact]
    public async Task ToggleEventCompletionAsync_ShouldThrowInvalidOperationException_WhenEventEnded()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarService(context.Repository);

        var pastEvent = CalendarTestContext.CreateEvent(
            context.UserId,
            "Ca học trong quá khứ",
            "Kiểm tra / Thi",
            DateTime.Now.AddHours(-3),
            DateTime.Now.AddHours(-1)
        );
        await context.Db.ScheduleEvents.AddAsync(pastEvent);
        await context.Db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ToggleEventCompletionAsync(context.UserId, pastEvent.ScheduleEventId));

        Assert.Contains("Ca học đã kết thúc", ex.Message);
    }

    [Fact]
    public async Task DeleteEventAsync_ShouldSoftDelete_AndCancelPendingReminders()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarService(context.Repository);

        var ev = CalendarTestContext.CreateEvent(
            context.UserId,
            "Sự kiện sắp xóa",
            "Bài tập / Nộp bài",
            DateTime.Now.AddDays(1),
            DateTime.Now.AddDays(1).AddHours(2)
        );
        await context.Db.ScheduleEvents.AddAsync(ev);
        await context.Db.SaveChangesAsync();

        var reminder1 = new Reminder
        {
            UserId = context.UserId,
            ScheduleEventId = ev.ScheduleEventId,
            OccurrenceDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            Channel = "InApp",
            RemindAtUtc = DateTime.UtcNow.AddDays(1),
            Status = "Pending"
        };
        var reminder2 = new Reminder
        {
            UserId = context.UserId,
            ScheduleEventId = ev.ScheduleEventId,
            OccurrenceDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            Channel = "Email",
            RemindAtUtc = DateTime.UtcNow.AddDays(1),
            Status = "Pending"
        };
        await context.Db.Reminders.AddRangeAsync(reminder1, reminder2);
        await context.Db.SaveChangesAsync();

        // Act
        await service.DeleteEventAsync(context.UserId, ev.ScheduleEventId);

        // Assert
        var deletedEvent = await context.Db.ScheduleEvents.FindAsync(ev.ScheduleEventId);
        Assert.NotNull(deletedEvent);
        Assert.True(deletedEvent.IsDeleted);

        var reminders = await context.Db.Reminders.Where(r => r.ScheduleEventId == ev.ScheduleEventId).ToListAsync();
        Assert.All(reminders, r => Assert.Equal("Cancelled", r.Status));
    }

    [Fact]
    public async Task GetCategoriesAsync_ShouldSeedDefaultCategories_WhenUserHasNoCategories()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarService(context.Repository);

        // Act
        var categories = await service.GetCategoriesAsync(context.UserId);

        // Assert
        Assert.Equal(4, categories.Count);
        Assert.Contains(categories, c => c.Name == "Tự học" && c.IsDefault);
        Assert.Contains(categories, c => c.Name == "Bài tập / Nộp bài" && c.IsDefault);
        Assert.Contains(categories, c => c.Name == "Kiểm tra / Thi" && c.IsDefault);
        Assert.Contains(categories, c => c.Name == "Học nhóm" && c.IsDefault);

        // Kiểm tra database đã lưu 4 danh mục mặc định
        var dbCategories = await context.Db.CalendarCategories.Where(c => c.UserId == context.UserId).ToListAsync();
        Assert.Equal(4, dbCategories.Count);
    }

    [Fact]
    public async Task CreateCategoryAsync_ShouldThrowInvalidOperationException_WhenCategoryNameAlreadyExists()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarService(context.Repository);

        // Thêm trước 1 danh mục "Học nhóm"
        await context.Db.CalendarCategories.AddAsync(new CalendarCategory
        {
            UserId = context.UserId,
            Name = "Học nhóm",
            IsDefault = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await context.Db.SaveChangesAsync();

        var duplicateDto = new SaveCalendarCategoryDto("học nhóm"); // Khác hoa thường

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCategoryAsync(context.UserId, duplicateDto));

        Assert.Contains("Loại sự kiện này đã tồn tại", ex.Message);
    }
}
