namespace StudyHub.BLL.DTOs;

public sealed record GradeEntryDto(
    long Id,
    long UserSubjectId,
    string Title,
    string AssessmentType,
    decimal Score,
    decimal MaxScore,
    decimal Weight,
    DateOnly AssessedOn);

public sealed record SubjectGradeSummaryDto(long UserSubjectId, string SubjectName, decimal? CurrentAverage10);
public sealed record GoalDto(long Id, string Title, string UnitCode, decimal TargetValue, decimal CurrentValue, DateOnly EndDate);
