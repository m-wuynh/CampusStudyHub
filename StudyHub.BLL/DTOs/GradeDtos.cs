namespace StudyHub.BLL.DTOs;

using System;
using System.Collections.Generic;

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

public sealed record GoalDto(
    long Id, 
    string Title, 
    string UnitCode, 
    decimal TargetValue, 
    decimal CurrentValue, 
    DateOnly EndDate,
    long? UserSubjectId,
    string SubjectName,
    bool IsCompleted);

public sealed record AcademicTermDto(long Id, string TermName, DateOnly StartDate, DateOnly EndDate);

public sealed record SubjectGradeDetailDto(
    long UserSubjectId, 
    string SubjectName, 
    string ColorHex, 
    decimal? TargetScore10, 
    decimal CreditWeight,
    decimal? CurrentAverage10, 
    List<GradeEntryDto> Entries);

public sealed record TermGradeSummaryDto(
    decimal? CurrentAverage10, 
    decimal? HighestScore, 
    string HighestSubjectName, 
    decimal? LowestScore, 
    string LowestSubjectName, 
    int SubjectsBelowTarget);

public sealed record GradesPageDto(
    List<AcademicTermDto> Terms, 
    long? SelectedTermId, 
    TermGradeSummaryDto Summary, 
    List<SubjectGradeDetailDto> SubjectDetails, 
    List<GoalDto> Goals);

public sealed record CreateGradeEntryDto(
    long UserSubjectId,
    string Title,
    string AssessmentType,
    decimal Score,
    decimal MaxScore,
    decimal Weight,
    DateOnly AssessedOn);

public sealed record UpdateGradeEntryDto(
    long Id,
    long UserSubjectId,
    string Title,
    string AssessmentType,
    decimal Score,
    decimal MaxScore,
    decimal Weight,
    DateOnly AssessedOn);

public sealed record CreateGoalDto(
    string Title,
    long? UserSubjectId,
    string UnitCode,
    decimal TargetValue,
    DateOnly EndDate);

public sealed record UpdateGoalDto(
    long Id,
    string Title,
    long? UserSubjectId,
    string UnitCode,
    decimal TargetValue,
    DateOnly EndDate);

public sealed record CreateTermDto(
    string TermName,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record UpdateTermDto(
    long Id,
    string TermName,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record CreateUserSubjectDto(
    long AcademicTermId,
    string SubjectName,
    decimal CreditWeight);

public sealed record UpdateUserSubjectDto(
    long UserSubjectId,
    string SubjectName,
    decimal? TargetScore10,
    decimal CreditWeight,
    string ColorHex);
