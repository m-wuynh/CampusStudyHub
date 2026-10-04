using StudyHub.BLL.DTOs;
using System.Collections.Generic;
using System.Linq;

namespace StudyHub.BLL.Services.Grades;

public static class GradeCalculator
{
    /// <summary>
    /// Computes weighted average on scale 0–10.
    /// Formula: SUM(Score × Weight) / SUM(Weight)
    /// Only counts entries belonging to active (non-archived) columns.
    /// Returns null if there are no valid entries (do NOT treat as 0).
    /// </summary>
    public static decimal? CalculateAverage10FromEntries(
        IEnumerable<GradeEntryDto> entries,
        IEnumerable<GradeColumnDto> allColumns)
    {
        // Build lookup of active column weights
        var activeColumnWeights = allColumns
            .ToDictionary(c => c.GradeColumnId, c => c.Weight);

        var validEntries = entries
            .Where(e => activeColumnWeights.ContainsKey(e.GradeColumnId))
            .Select(e => (Score: e.Score, Weight: activeColumnWeights[e.GradeColumnId]))
            .ToList();

        if (!validEntries.Any()) return null;

        var totalWeight = validEntries.Sum(e => e.Weight);
        if (totalWeight == 0) return null;

        var weightedSum = validEntries.Sum(e => e.Score * e.Weight);
        return Math.Round(weightedSum / totalWeight, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Compute Goal Ladder progress percentage.
    /// Cap at 100% — being above target does not count as over 100%.
    /// Returns null if currentValue is null (no grade data yet).
    /// </summary>
    public static int? ComputeProgressPercent(decimal? currentValue, decimal targetValue)
    {
        if (currentValue is null) return null;
        if (targetValue <= 0) return null;

        var pct = currentValue.Value / targetValue * 100m;
        return (int)Math.Min(100m, Math.Floor(pct));
    }
}
