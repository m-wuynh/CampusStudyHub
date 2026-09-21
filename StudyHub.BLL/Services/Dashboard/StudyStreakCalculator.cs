namespace StudyHub.BLL.Services.Dashboard;

public static class StudyStreakCalculator
{
    public static (int Current, int Longest) Calculate(IEnumerable<DateOnly> studyDates, DateOnly today)
    {
        var dates = studyDates.Distinct().Order().ToArray();
        if (dates.Length == 0) return (0, 0);

        var longest = 1;
        var running = 1;
        for (var index = 1; index < dates.Length; index++)
        {
            running = dates[index].DayNumber == dates[index - 1].DayNumber + 1 ? running + 1 : 1;
            longest = Math.Max(longest, running);
        }

        var lastDate = dates[^1];
        if (lastDate != today && lastDate != today.AddDays(-1)) return (0, longest);

        var current = 1;
        for (var index = dates.Length - 1; index > 0; index--)
        {
            if (dates[index].DayNumber != dates[index - 1].DayNumber + 1) break;
            current++;
        }

        return (current, longest);
    }
}
