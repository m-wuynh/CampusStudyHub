namespace StudyHub.Web.ViewModels.Dashboard;

public sealed record DashboardIndexViewModel(
    string DisplayName, 
    int StudyStreakDays,
    int WeeklyTotalEvents,
    int WeeklyCompletedEvents,
    int WeeklyMissedEvents);
