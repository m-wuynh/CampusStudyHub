namespace StudyHub.Web.ViewModels.Dashboard;

using StudyHub.BLL.DTOs;
using System;
using System.Collections.Generic;

public sealed record DashboardIndexViewModel(
    string DisplayName,
    string FormattedDateText,
    string? AcademicTermLabel,
    string TimeGreeting,
    int TodayTasksCount,
    int TodayCompletedTasksCount,
    UpcomingEventSummaryDto? NextExam,
    decimal? CurrentAverage10,
    string? CurrentRankText,
    GoalSummaryDto? ActiveGoalSummary,
    List<ScheduleEventDto> TodaySchedule,
    List<UpcomingEventSummaryDto> UpcomingEvents,
    FlashcardSummaryDto FlashcardSummary
);

public sealed record UpcomingEventSummaryDto(
    long EventId,
    string Title,
    string EventType,
    string EventTypeLabel,
    string BadgeCssClass,
    string DateText,
    string TimeText,
    int? DaysLeftText
);

public sealed record GoalSummaryDto(
    decimal? CurrentAverage10,
    decimal TargetAverage,
    int ProgressPercent,
    List<HighlightedGoalDto> Goals
);

public sealed record HighlightedGoalDto(
    string SubjectName,
    decimal TargetValue,
    decimal? CurrentValue,
    int ProgressPercent
);

public sealed record FlashcardSummaryDto(
    int TotalDecks,
    int TotalCards,
    List<FlashcardDeckSummaryDto> Decks
);
