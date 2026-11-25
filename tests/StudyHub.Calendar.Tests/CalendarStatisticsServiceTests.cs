using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StudyHub.BLL.Services.Calendar;
using StudyHub.DAL.Entities;
using Xunit;

namespace StudyHub.Calendar.Tests;

public sealed class CalendarStatisticsServiceTests
{
    [Fact]
    public async Task GetWeeklyProgressAsync_ShouldCalculateCorrectPercentage_WhenEventsExist()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarStatisticsService(context.Repository);

        var today = DateOnly.FromDateTime(DateTime.Today);
        int diff = (int)today.DayOfWeek;
        var startOfWeek = today.AddDays(-diff);
        var targetDate = startOfWeek.AddDays(2); // Giữa tuần

        // 4 ca đã hoàn thành
        for (int i = 0; i < 4; i++)
        {
            await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
                context.UserId,
                $"Ca hoàn thành {i + 1}",
                "Tự học",
                startOfWeek.ToDateTime(new TimeOnly(8 + i, 0)),
                startOfWeek.ToDateTime(new TimeOnly(9 + i, 0)),
                isCompleted: true
            ));
        }

        // 7 ca quá hạn (kết thúc hôm qua, chưa hoàn thành)
        var yesterday = DateTime.Now.AddDays(-1);
        for (int i = 0; i < 7; i++)
        {
            await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
                context.UserId,
                $"Ca quá hạn {i + 1}",
                "Học nhóm",
                yesterday.Date.AddHours(7 + i),
                yesterday.Date.AddHours(8 + i),
                isCompleted: false
            ));
        }

        // 20 ca sắp tới (kết thúc ngày mai, chưa hoàn thành)
        var tomorrow = DateTime.Now.AddDays(1);
        for (int i = 0; i < 20; i++)
        {
            await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
                context.UserId,
                $"Ca sắp tới {i + 1}",
                "Kiểm tra / Thi",
                tomorrow.Date.AddHours(8),
                tomorrow.Date.AddHours(9),
                isCompleted: false
            ));
        }

        await context.Repository.SaveChangesAsync();

        // Act
        var progress = await service.GetWeeklyProgressAsync(context.UserId, targetDate);

        // Assert: 4 / 31 ca = 12.9% -> 13%
        Assert.Equal(31, progress.TotalEvents);
        Assert.Equal(4, progress.CompletedEvents);
        Assert.Equal(7, progress.MissedEvents);
        Assert.Equal(20, progress.UpcomingEvents);
        Assert.Equal(13, progress.CompletionPct);
    }

    [Fact]
    public async Task GetWeeklyProgressAsync_ShouldReturnZeroPercentage_WhenNoEventsExist()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarStatisticsService(context.Repository);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var progress = await service.GetWeeklyProgressAsync(context.UserId, today);

        // Assert
        Assert.Equal(0, progress.TotalEvents);
        Assert.Equal(0, progress.CompletedEvents);
        Assert.Equal(0, progress.MissedEvents);
        Assert.Equal(0, progress.UpcomingEvents);
        Assert.Equal(0, progress.CompletionPct);
    }

    [Fact]
    public async Task GetWeeklyProgressAsync_ShouldFilterByCategory_WhenCategorySpecified()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarStatisticsService(context.Repository);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var baseDate = today.ToDateTime(new TimeOnly(10, 0));

        // 2 ca Tự học
        await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
            context.UserId, "Tự học Toán", "Tự học", baseDate, baseDate.AddHours(1), isCompleted: true));
        await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
            context.UserId, "Tự học Lý", "Tự học", baseDate.AddHours(2), baseDate.AddHours(3), isCompleted: false));

        // 1 ca Học nhóm
        await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
            context.UserId, "Học nhóm Hóa", "Học nhóm", baseDate.AddHours(4), baseDate.AddHours(5), isCompleted: true));

        await context.Repository.SaveChangesAsync();

        // Act
        var progressTuHoc = await service.GetWeeklyProgressAsync(context.UserId, today, "Tự học");
        var progressHocNhom = await service.GetWeeklyProgressAsync(context.UserId, today, "Học nhóm");

        // Assert
        Assert.Equal(2, progressTuHoc.TotalEvents);
        Assert.Equal(1, progressTuHoc.CompletedEvents);
        Assert.Equal(50, progressTuHoc.CompletionPct);

        Assert.Equal(1, progressHocNhom.TotalEvents);
        Assert.Equal(1, progressHocNhom.CompletedEvents);
        Assert.Equal(100, progressHocNhom.CompletionPct);
    }

    [Fact]
    public async Task GetReportsAsync_ShouldGeneratePomodoroRecommendation_WhenCompletionRateLow()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarStatisticsService(context.Repository);

        var pastDate = DateTime.Now.AddDays(-2);

        // Tạo 1 ca hoàn thành, 9 ca quá hạn -> 10%
        await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
            context.UserId, "Ca 1", "Tự học", pastDate, pastDate.AddHours(1), isCompleted: true));

        for (int i = 0; i < 9; i++)
        {
            await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
                context.UserId, $"Ca quá hạn {i}", "Tự học", pastDate, pastDate.AddHours(1), isCompleted: false));
        }
        await context.Repository.SaveChangesAsync();

        // Act
        var reports = await service.GetReportsAsync(context.UserId, "this-week");

        // Assert
        Assert.Equal("Cần chú ý", reports.InsightBadge);
        Assert.Equal("warning", reports.InsightBadgeType);
        Assert.Contains("Pomodoro", reports.InsightRecommendation);
    }

    [Fact]
    public async Task GetReportsAsync_ShouldGenerateExcellentRecommendation_WhenCompletionRateHigh()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarStatisticsService(context.Repository);

        var today = DateTime.Now;

        // Tạo 9 ca hoàn thành / 10 ca -> 90%
        for (int i = 0; i < 9; i++)
        {
            await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
                context.UserId, $"Ca hoàn thành {i}", "Tự học", today, today.AddHours(1), isCompleted: true));
        }
        await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
            context.UserId, "Ca sắp tới", "Tự học", today.AddDays(1), today.AddDays(1).AddHours(1), isCompleted: false));
        
        await context.Repository.SaveChangesAsync();

        // Act
        var reports = await service.GetReportsAsync(context.UserId, "this-week");

        // Assert
        Assert.Equal("Xuất sắc", reports.InsightBadge);
        Assert.Equal("success", reports.InsightBadgeType);
        Assert.Equal(90, reports.CompletionPct);
    }

    [Fact]
    public async Task GetReportsAsync_ShouldGroupByCategoryDistribution()
    {
        // Arrange
        await using var context = new CalendarTestContext();
        var service = new CalendarStatisticsService(context.Repository);

        // Seed 2 danh mục
        await context.Repository.AddAsync(new CalendarCategory { UserId = context.UserId, Name = "Tự học", IsDefault = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await context.Repository.AddAsync(new CalendarCategory { UserId = context.UserId, Name = "Học nhóm", IsDefault = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });

        var today = DateTime.Now;

        // 3 ca Tự học, 1 ca Học nhóm -> Tổng 4 ca
        for (int i = 0; i < 3; i++)
        {
            await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
                context.UserId, $"Tự học {i}", "Tự học", today, today.AddHours(1), isCompleted: true));
        }
        await context.Repository.AddAsync(CalendarTestContext.CreateEvent(
            context.UserId, "Học nhóm 1", "Học nhóm", today, today.AddHours(1), isCompleted: false));

        await context.Repository.SaveChangesAsync();

        // Act
        var reports = await service.GetReportsAsync(context.UserId, "this-week");

        // Assert
        Assert.Equal(4, reports.TotalEvents);
        Assert.NotNull(reports.CategoryStats);
        Assert.True(reports.CategoryStats.Count >= 2);

        var tuHocStat = reports.CategoryStats.Find(c => c.CategoryName == "Tự học");
        Assert.NotNull(tuHocStat);
        Assert.Equal(3, tuHocStat.TotalEvents);
        Assert.Equal(75.0, tuHocStat.PercentageOfTotal);
        Assert.Equal(100, tuHocStat.CompletionPct);
    }
}
