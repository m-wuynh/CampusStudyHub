using StudyHub.BLL.DTOs;
using System;
using System.Threading.Tasks;

namespace StudyHub.BLL.Services.Calendar;

public interface ICalendarStatisticsService
{
    /// <summary>
    /// Tính toán tiến độ tuần (Tổng ca, Đã hoàn thành, Quá hạn, Sắp tới, Tỷ lệ %)
    /// </summary>
    Task<WeeklyProgressDto> GetWeeklyProgressAsync(long userId, DateOnly targetDate, string? category = null);

    /// <summary>
    /// Tính toán toàn bộ số liệu báo cáo & thống kê học tập (KPIs, Phân bổ theo loại ca, Lời khuyên thông minh)
    /// </summary>
    Task<CalendarReportsDto> GetReportsAsync(long userId, string range);
}
