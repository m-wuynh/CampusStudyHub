using StudyHub.BLL.DTOs;

namespace StudyHub.Web.ViewModels.Calendar;

public sealed class CalendarReportsViewModel : CalendarReportsDto
{
    public CalendarReportsViewModel() { }

    public CalendarReportsViewModel(CalendarReportsDto dto)
    {
        Range = dto.Range;
        RangeLabel = dto.RangeLabel;
        DateRangeText = dto.DateRangeText;
        TotalEvents = dto.TotalEvents;
        AvgPerDay = dto.AvgPerDay;
        CompletedEvents = dto.CompletedEvents;
        CompletionPct = dto.CompletionPct;
        MissedEvents = dto.MissedEvents;
        UpcomingEvents = dto.UpcomingEvents;
        CategoryStats = dto.CategoryStats;
        InsightTitle = dto.InsightTitle;
        InsightMessage = dto.InsightMessage;
        InsightBadge = dto.InsightBadge;
        InsightBadgeType = dto.InsightBadgeType;
        InsightRecommendation = dto.InsightRecommendation;
    }
}
