using StudyHub.BLL.Constants;
using StudyHub.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StudyHub.BLL.Services.Grades;

/// <summary>
/// Nơi duy nhất chứa công thức tính điểm của Grades & Goals.
/// </summary>
public static class GradeCalculator
{
    /// <summary>
    /// ĐTB môn (thang 10) từ entity. Bỏ qua điểm <c>IsDeleted</c> và cột <c>IsArchived</c>.
    /// Công thức: Σ(Score × Weight cột) / Σ(Weight cột) trên từng điểm. Không có điểm hợp lệ → null.
    /// </summary>
    public static decimal? CalculateSubjectAverage(IEnumerable<GradeEntry> entries, IEnumerable<GradeColumn> columns)
    {
        var weights = columns
            .Where(c => !c.IsArchived)
            .ToDictionary(c => c.GradeColumnId, c => c.Weight);

        return CalculateWeightedAverage(
            entries.Where(e => !e.IsDeleted).Select(e => (e.GradeColumnId, e.Score)),
            weights);
    }

    /// <summary>
    /// Lõi tính ĐTB có trọng số. Điểm thuộc cột không có trong <paramref name="columnWeights"/> bị bỏ qua.
    /// Làm tròn <see cref="GradeConstants.AverageDecimals"/> chữ số (AwayFromZero). Không có điểm hợp lệ → null.
    /// </summary>
    public static decimal? CalculateWeightedAverage(
        IEnumerable<(long ColumnId, decimal Score)> scores,
        IReadOnlyDictionary<long, decimal> columnWeights)
    {
        decimal weightedSum = 0m;
        decimal totalWeight = 0m;

        foreach (var (columnId, score) in scores)
        {
            if (!columnWeights.TryGetValue(columnId, out var weight)) continue;
            weightedSum += score * weight;
            totalWeight += weight;
        }

        if (totalWeight == 0m) return null;
        return Math.Round(weightedSum / totalWeight, GradeConstants.AverageDecimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// ĐTB chung: Σ(ĐTB môn × CreditWeight) / Σ CreditWeight trên các môn có điểm và CreditWeight &gt; 0.
    /// </summary>
    public static decimal? CalculateOverallAverage(IEnumerable<(decimal? Average, decimal CreditWeight)> subjects)
    {
        var valid = subjects.Where(s => s.Average.HasValue && s.CreditWeight > 0).ToList();
        if (valid.Count == 0) return null;

        var weightedSum = valid.Sum(s => s.Average!.Value * s.CreditWeight);
        var totalWeight = valid.Sum(s => s.CreditWeight);
        return Math.Round(weightedSum / totalWeight, GradeConstants.AverageDecimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>Làm tròn ĐTB để hiển thị (<see cref="GradeConstants.DisplayDecimals"/> chữ số, AwayFromZero).</summary>
    public static decimal? RoundForDisplay(decimal? value) =>
        value.HasValue
            ? Math.Round(value.Value, GradeConstants.DisplayDecimals, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>
    /// % tiến độ Goal Ladder: floor(ĐTB / Target × 100), tối đa 100. Chưa có điểm hoặc Target ≤ 0 → null.
    /// </summary>
    public static int? ComputeProgressPercent(decimal? currentValue, decimal targetValue)
    {
        if (currentValue is null) return null;
        if (targetValue <= 0) return null;

        var pct = currentValue.Value / targetValue * 100m;
        return (int)Math.Min(100m, Math.Floor(pct));
    }

    /// <summary>
    /// Điểm tối thiểu (bước 0.1) cần đạt ở MỖI cột còn trống để ĐTB hiển thị ≥ <paramref name="target"/>.
    /// Dùng đúng công thức và cách làm tròn ở trên. 0 → chắc chắn đạt; null → không còn cột trống hoặc kể cả 10 điểm cũng không đạt.
    /// </summary>
    public static decimal? ComputeRequiredScore(decimal knownWeightedSum, decimal knownWeight, decimal remainingWeight, decimal target)
    {
        if (remainingWeight <= 0) return null;

        for (var tenths = 0; tenths <= 100; tenths++)
        {
            var score = tenths / 10m;
            var average = Math.Round(
                (knownWeightedSum + score * remainingWeight) / (knownWeight + remainingWeight),
                GradeConstants.AverageDecimals,
                MidpointRounding.AwayFromZero);
            if (RoundForDisplay(average) >= target) return score;
        }
        return null;
    }

    /// <summary>
    /// Tách dữ liệu của một môn: Σ(điểm × hệ số), Σ hệ số của các điểm đã có, và tổng hệ số các cột chưa có điểm nào.
    /// </summary>
    public static (decimal KnownWeightedSum, decimal KnownWeight, decimal RemainingWeight) SubjectWeights(
        IEnumerable<(long ColumnId, decimal Score)> scores,
        IReadOnlyDictionary<long, decimal> columnWeights)
    {
        decimal weightedSum = 0m;
        decimal knownWeight = 0m;
        var usedColumns = new HashSet<long>();

        foreach (var (columnId, score) in scores)
        {
            if (!columnWeights.TryGetValue(columnId, out var weight)) continue;
            weightedSum += score * weight;
            knownWeight += weight;
            usedColumns.Add(columnId);
        }

        var remainingWeight = columnWeights.Where(c => !usedColumns.Contains(c.Key)).Sum(c => c.Value);
        return (weightedSum, knownWeight, remainingWeight);
    }
}
