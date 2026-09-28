using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Grades;

public static class GradeCalculator
{
    public static decimal? CalculateAverage10(IEnumerable<GradeEntryDto> entries)
    {
        var validEntries = entries
            .Where(entry => entry.MaxScore > 0 && entry.Weight > 0)
            .ToList();

        var totalWeight = validEntries.Sum(entry => entry.Weight);
        if (totalWeight == 0) return null;

        var weightedScore = validEntries.Sum(entry =>
            entry.Score / entry.MaxScore * 10m * entry.Weight);

        return Math.Round(weightedScore / totalWeight, 2, MidpointRounding.AwayFromZero);
    }
}
