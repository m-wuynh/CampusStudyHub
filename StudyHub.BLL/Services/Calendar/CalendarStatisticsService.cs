using StudyHub.BLL.DTOs;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudyHub.BLL.Services.Calendar;

public sealed class CalendarStatisticsService(IRepository repository) : ICalendarStatisticsService
{
    public async Task<WeeklyProgressDto> GetWeeklyProgressAsync(long userId, DateOnly targetDate, string? category = null)
    {
        // 1. Xác định 7 ngày trong tuần của targetDate (Chủ nhật đến Thứ bảy)
        int diff = (int)targetDate.DayOfWeek;
        var start = targetDate.AddDays(-diff);
        var end = start.AddDays(6);

        // 2. Lấy toàn bộ sự kiện chưa bị xóa của user
        var entities = await repository.ListAsync<ScheduleEvent>(e => e.UserId == userId && !e.IsDeleted);

        var eventDtos = entities
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
            .ToList();

        var now = DateTime.Now;
        int total = 0;
        int completed = 0;
        int missed = 0;
        int upcoming = 0;

        bool hasCategoryFilter = !string.IsNullOrWhiteSpace(category) && !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase);

        foreach (var ev in eventDtos)
        {
            if (hasCategoryFilter && !string.Equals(ev.EventType, category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var starts = ScheduleOccurrenceService.GetStarts(ev, start, end);
            var duration = ev.EndAtLocal - ev.StartAtLocal;

            foreach (var startDt in starts)
            {
                total++;
                var endDt = startDt.Add(duration);

                if (ev.IsCompleted)
                {
                    completed++;
                }
                else if (endDt < now)
                {
                    missed++;
                }
                else
                {
                    upcoming++;
                }
            }
        }

        // 3. Tính phần trăm hoàn thành 
        int completionPct = total > 0 ? (int)Math.Round((double)completed * 100.0 / total) : 0;

        return new WeeklyProgressDto(total, completed, missed, upcoming, completionPct);
    }

    public async Task<CalendarReportsDto> GetReportsAsync(long userId, string range)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);

        DateOnly start;
        DateOnly end;
        string rangeLabel;
        int numDays;

        switch (range?.ToLowerInvariant())
        {
            case "this-year":
                start = new DateOnly(today.Year, 1, 1);
                end = new DateOnly(today.Year, 12, 31);
                rangeLabel = "Năm này";
                numDays = DateTime.IsLeapYear(today.Year) ? 366 : 365;
                break;

            case "this-month":
                start = new DateOnly(today.Year, today.Month, 1);
                end = start.AddMonths(1).AddDays(-1);
                rangeLabel = "Tháng này";
                numDays = DateTime.DaysInMonth(today.Year, today.Month);
                break;

            case "this-week":
            default:
                range = "this-week";
                var diff = (int)today.DayOfWeek;
                start = today.AddDays(-diff);
                end = start.AddDays(6);
                rangeLabel = "Tuần này";
                numDays = 7;
                break;
        }

        var entities = await repository.ListAsync<ScheduleEvent>(e => e.UserId == userId && !e.IsDeleted);
        var events = entities
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
            .ToList();

        int totalEvents = 0;
        int completedEvents = 0;
        int missedEvents = 0;
        int upcomingEvents = 0;

        string NormalizeCategoryName(string? type) =>
            string.IsNullOrWhiteSpace(type) ? "Khác" : type.Trim();

        var categories = await repository.ListAsync<CalendarCategory>(c => c.UserId == userId);
        var categoryDict = new Dictionary<string, (int Total, int Completed, int Missed)>(StringComparer.OrdinalIgnoreCase);

        foreach (var cat in categories)
        {
            var normalized = NormalizeCategoryName(cat.Name);
            if (!categoryDict.ContainsKey(normalized))
            {
                categoryDict[normalized] = (0, 0, 0);
            }
        }

        foreach (var ev in events)
        {
            var starts = ScheduleOccurrenceService.GetStarts(ev, start, end);
            var duration = ev.EndAtLocal - ev.StartAtLocal;
            var catName = NormalizeCategoryName(ev.EventType);

            foreach (var startDt in starts)
            {
                totalEvents++;
                var endDt = startDt.Add(duration);
                bool isCompleted = ev.IsCompleted;
                bool isMissed = !isCompleted && endDt < now;

                if (!categoryDict.TryGetValue(catName, out var stats))
                {
                    stats = (0, 0, 0);
                }

                stats.Total++;
                if (isCompleted)
                {
                    completedEvents++;
                    stats.Completed++;
                }
                else if (isMissed)
                {
                    missedEvents++;
                    stats.Missed++;
                }
                else
                {
                    upcomingEvents++;
                }

                categoryDict[catName] = stats;
            }
        }

        var categoryStats = new List<CategoryStatItemDto>();
        foreach (var kvp in categoryDict.OrderByDescending(k => k.Value.Total).ThenBy(k => k.Key))
        {
            int catTotal = kvp.Value.Total;
            int catCompleted = kvp.Value.Completed;
            int catMissed = kvp.Value.Missed;
            double pctOfTotal = totalEvents > 0 ? Math.Round((double)catTotal * 100.0 / totalEvents, 1) : 0;
            int completionPctCat = catTotal > 0 ? (int)Math.Round((double)catCompleted * 100.0 / catTotal) : 0;

            string statusBadge;
            string statusBadgeType;

            if (catTotal == 0)
            {
                statusBadge = "Chưa có ca";
                statusBadgeType = "secondary";
            }
            else if (completionPctCat >= 80)
            {
                statusBadge = "Xuất sắc";
                statusBadgeType = "success";
            }
            else if (completionPctCat >= 50)
            {
                statusBadge = "Khá tốt";
                statusBadgeType = "primary";
            }
            else
            {
                statusBadge = catMissed > 0 ? "Cần chú ý" : "Đang tiến hành";
                statusBadgeType = catMissed > 0 ? "warning" : "secondary";
            }

            categoryStats.Add(new CategoryStatItemDto
            {
                CategoryName = kvp.Key,
                TotalEvents = catTotal,
                PercentageOfTotal = pctOfTotal,
                CompletedEvents = catCompleted,
                CompletionPct = completionPctCat,
                MissedEvents = catMissed,
                StatusBadge = statusBadge,
                StatusBadgeType = statusBadgeType
            });
        }

        double avgPerDay = numDays > 0 ? Math.Round((double)totalEvents / numDays, 1) : 0;
        int completionPct = totalEvents == 0 ? 0 : (int)Math.Round((double)completedEvents * 100.0 / totalEvents);

        // Smart Insight Generation
        string insightTitle;
        string insightMessage;
        string insightBadge;
        string insightBadgeType;
        string insightRecommendation;

        if (totalEvents == 0)
        {
            insightTitle = "Chưa có dữ liệu ca học";
            insightBadge = "Chưa có dữ liệu";
            insightBadgeType = "secondary";
            insightMessage = $"Không có lịch học hoặc nhiệm vụ nào được ghi nhận trong {rangeLabel.ToLower()} ({start:dd/MM} - {end:dd/MM/yyyy}).";
            insightRecommendation = "Hãy lên lịch các ca học hoặc bài tập mới để StudyHub đồng hành và phân tích hiệu suất cho bạn!";
        }
        else if (completionPct >= 80)
        {
            insightTitle = "Phong độ học tập xuất sắc!";
            insightBadge = "Xuất sắc";
            insightBadgeType = "success";
            insightMessage = $"Bạn đã hoàn thành {completedEvents}/{totalEvents} ca học ({completionPct}%) với cường độ trung bình {avgPerDay} ca/ngày. Kỷ luật học tập và khả năng quản lý thời gian của bạn rất đáng biểu dương!";
            insightRecommendation = missedEvents > 0
                ? $"Lưu ý: Có {missedEvents} ca học đã quá hạn. Hãy tranh thủ ôn tập nhanh để củng cố toàn bộ kiến thức nhé!"
                : "Tuyệt vời! Không có ca học nào bị quá hạn. Hãy duy trì nhịp độ ổn định này và tự thưởng cho mình một khoảng thời gian thư giãn hợp lý.";
        }
        else if (completionPct >= 50)
        {
            insightTitle = "Tiến độ học tập khá ổn định";
            insightBadge = "Khá tốt";
            insightBadgeType = "primary";
            insightMessage = $"Bạn đã hoàn thành {completedEvents}/{totalEvents} ca học ({completionPct}%). Bạn đang đi đúng hướng và nắm giữ tiến độ học tập tương đối tốt.";
            insightRecommendation = missedEvents > 0
                ? $"Cảnh báo: Hiện có {missedEvents} ca học quá hạn. Bạn nên ưu tiên hoàn thành bù các nội dung này trước khi bắt đầu bài học mới."
                : $"Bạn còn {upcomingEvents} ca học sắp tới. Hãy phân bổ thời gian hợp lý để nâng tỷ lệ hoàn thành lên trên 80% nhé!";
        }
        else
        {
            insightTitle = "Cần tăng tốc và cải thiện tiến độ";
            insightBadge = "Cần chú ý";
            insightBadgeType = "warning";
            insightMessage = $"Tỷ lệ hoàn thành hiện tại đạt {completionPct}% ({completedEvents}/{totalEvents} ca). Bạn đang bị dồn ứ một số lượng bài học và kế hoạch cần xử lý.";
            insightRecommendation = missedEvents > 0
                ? $"Có {missedEvents} ca học đã quá hạn cần bổ sung lại kiến thức ngay. Hãy thử áp dụng phương pháp Pomodoro (25 phút tập trung) để xử lý dứt điểm từng phần nhỏ mà không bị áp lực."
                : "Hãy bắt tay vào hoàn thành các ca học sắp tới để nhanh chóng lấy lại nhịp độ học tập hiệu quả!";
        }

        var dateRangeText = $"{start:dd/MM/yyyy} - {end:dd/MM/yyyy}";

        return new CalendarReportsDto
        {
            Range = range,
            RangeLabel = rangeLabel,
            DateRangeText = dateRangeText,
            TotalEvents = totalEvents,
            AvgPerDay = avgPerDay,
            CompletedEvents = completedEvents,
            CompletionPct = completionPct,
            MissedEvents = missedEvents,
            UpcomingEvents = upcomingEvents,
            CategoryStats = categoryStats,
            InsightTitle = insightTitle,
            InsightMessage = insightMessage,
            InsightBadge = insightBadge,
            InsightBadgeType = insightBadgeType,
            InsightRecommendation = insightRecommendation
        };
    }
}
