namespace StudyHub.BLL.DTOs;

using System;
using System.Collections.Generic;
using StudyHub.BLL.Constants;

// ── Grade Book ──────────────────────────────────────────────
public sealed record GradeBookDto(long GradeBookId, string Name, int FromYear, int ToYear);

// ── Grade Column ─────────────────────────────────────────────
public sealed record GradeColumnDto(
    long GradeColumnId,
    long GradeBookId,
    string Name,
    decimal Weight,
    int DisplayOrder);

// ── Grade Entry ──────────────────────────────────────────────
/// <summary>Score is always on scale 0–10.</summary>
public sealed record GradeEntryDto(
    long Id,
    long UserSubjectId,
    long GradeColumnId,
    decimal Score);

// ── Summary views ────────────────────────────────────────────
public sealed record SubjectGradeDetailDto(
    long UserSubjectId,
    string SubjectName,
    string ColorHex,
    decimal CreditWeight,
    decimal? CurrentAverage10,
    List<GradeEntryDto> Entries);

public sealed record YearGradeSummaryDto(
    decimal? CurrentAverage10,
    decimal? HighestScore,
    string HighestSubjectName,
    decimal? LowestScore,
    string LowestSubjectName,
    int SubjectsBelowTarget);

// ── Grade Subject Option (for lookup across academic years) ──
public sealed record GradeSubjectOptionDto(
    long UserSubjectId,
    long? GradeBookId,
    string SubjectName);

// ── Full page payload ────────────────────────────────────────
public sealed record GradesPageDto(
    List<GradeBookDto> GradeBooks,
    long? SelectedGradeBookId,
    GradeBookDto? GradeBook,
    List<GradeColumnDto> Columns,
    YearGradeSummaryDto Summary,
    List<SubjectGradeDetailDto> SubjectDetails,
    List<GoalLadderDto> GoalLadders,
    List<GradeSubjectOptionDto> AllSubjects);

// ── Goal Ladder ──────────────────────────────────────────────
public sealed record GoalActivityDto(
    long Id,
    long GoalId,
    string Title,
    DateTime StartAtLocal,
    DateTime EndAtLocal,
    string? Description,
    bool IsCompleted);

public sealed record GoalHistoryDto(
    long Id,
    long GoalId,
    decimal TargetValue,
    decimal AchievedScore,
    DateTime AchievedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>All goals for a single UserSubject, ordered by BaseGoal first, then additional rungs.</summary>
public sealed record GoalLadderDto(
    long UserSubjectId,
    string SubjectName,
    decimal? CurrentAverage10,
    List<GoalDto> Goals,
    bool HasBaseGoal = false,
    GoalDto? BaseGoal = null,
    List<GoalDto>? AdditionalGoals = null,
    string? AcademicYearName = null,
    bool IsDetached = false);

/// <summary>
/// Represents a single rung on the Goal Ladder.
/// CurrentValue is derived from the subject's CurrentAverage10 – never user-entered.
/// Status can be ToDo, InProgress, Done (or mapped to GoalStatus).
/// ProgressPercent do BLL tính theo ĐTB hiển thị (Q08); view và Dashboard không tự tính.
/// </summary>
public sealed record GoalDto(
    long Id,
    string Title,
    decimal TargetValue,
    decimal? CurrentValue,
    DateOnly? EndDate,
    long UserSubjectId,
    GoalStatus Status,
    bool IsBaseGoal = false,
    string GoalLifecycleStatus = GradeConstants.GoalLifecycle.ToDo,
    int DisplayOrder = 0,
    decimal? AchievedScore = null,
    DateTime? AchievedAtUtc = null,
    List<GoalActivityDto>? Activities = null,
    List<GoalHistoryDto>? Histories = null,
    int? ProgressPercent = null,
    bool IsOverdue = false);

// ── Dashboard: tổng quan mục tiêu ───────────────────────────
public sealed record GoalHighlightDto(string SubjectName, decimal TargetValue, decimal? CurrentValue, int ProgressPercent);
public sealed record GoalOverviewDto(int OverallProgressPercent, decimal TargetAverage, List<GoalHighlightDto> Highlights);

/// <summary>Ánh xạ 1-1 từ trạng thái lifecycle (Done / InProgress / ToDo), xem GoalStatusEvaluator.ToLegacyStatus.</summary>
public enum GoalStatus
{
    /// <summary>Done: ĐTB hiển thị ≥ mục tiêu.</summary>
    Achieved,
    /// <summary>InProgress: đã có điểm nhưng chưa đạt.</summary>
    InProgress,
    /// <summary>ToDo: chưa có điểm.</summary>
    NoData
}

// ── Command DTOs ─────────────────────────────────────────────

// GradeBook
public sealed record UpdateGradeBookDto(long GradeBookId, string Name);

// GradeColumn
public sealed record CreateGradeColumnDto(long GradeBookId, string Name, decimal Weight, int DisplayOrder);
public sealed record UpdateGradeColumnDto(long GradeColumnId, string Name, decimal Weight, int DisplayOrder);

// GradeEntry
public sealed record CreateGradeEntryDto(long UserSubjectId, long GradeColumnId, decimal Score);
public sealed record UpdateGradeEntryDto(long Id, decimal Score);
public sealed record GradeEntryInputDto(long? GradeEntryId, long GradeColumnId, decimal Score);
public sealed record SaveSubjectGradesDto(long UserSubjectId, List<GradeEntryInputDto> Entries);

// Goal & Activities
public sealed record CreateGoalDto(
    long UserSubjectId,
    string Title,
    decimal TargetValue,
    DateOnly? EndDate,
    bool IsBaseGoal = false);

public sealed record UpdateGoalDto(
    long Id,
    string Title,
    decimal? TargetValue,
    DateOnly? EndDate);

public sealed record ReorderGoalDto(long GoalId, int NewDisplayOrder);

public sealed record CreateGoalActivityDto(
    long GoalId,
    string Title,
    DateTime StartAtLocal,
    DateTime EndAtLocal,
    string? Description);

public sealed record UpdateGoalActivityDto(
    long ActivityId,
    string Title,
    DateTime StartAtLocal,
    DateTime EndAtLocal,
    string? Description);

// UserSubject
public sealed record CreateUserSubjectDto(long GradeBookId, string SubjectName, decimal CreditWeight);
public sealed record UpdateUserSubjectDto(long UserSubjectId, string SubjectName, decimal CreditWeight, string ColorHex);

// ── Wizard DTOs ──────────────────────────────────────────────
public sealed record CreateColumnInitDto(string Name, decimal Weight);
public sealed record CreateGradeBookWithColumnsDto(
    int FromYear,
    int ToYear,
    string Name,
    List<CreateColumnInitDto> Columns,
    List<string> SubjectNames);

// ── Reorder DTOs ─────────────────────────────────────────────
public sealed record ReorderColumnDto(long GradeColumnId, int NewDisplayOrder);
public sealed record ReorderSubjectDto(long UserSubjectId, int NewDisplayOrder);

