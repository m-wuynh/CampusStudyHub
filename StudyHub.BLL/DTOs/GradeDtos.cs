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
    bool IsDetached = false,
    GoalJourneyDto? Journey = null);

// ── Hành trình mục tiêu (GoalProgressLogs) ──────────────────
/// <summary>Một mốc trong hành trình của môn. Điểm số đã làm tròn hiển thị; thời gian theo giờ ứng dụng.</summary>
public sealed record GoalJourneyEventDto(
    long Id,
    string EventType,
    string Category,
    long? GoalId,
    string? GoalTitle,
    bool IsBaseGoal,
    decimal? AverageScore,
    decimal? PreviousValue,
    decimal? TargetValue,
    int? ProgressPercent,
    string? StatusFrom,
    string? StatusTo,
    DateTime OccurredAtLocal)
{
    public const string CategoryScore = "score";
    public const string CategoryGoal = "goal";
}

public sealed record GoalJourneyPointDto(DateTime AtLocal, decimal Average);

/// <summary>Events: mới nhất trước. Points: tăng dần theo thời gian (vẽ biểu đồ ĐTB).</summary>
public sealed record GoalJourneyDto(List<GoalJourneyEventDto> Events, List<GoalJourneyPointDto> Points);

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


// ── Màn "Tổng quan năm học" ─────────────────────────────────
/// <summary>Tóm tắt một sổ điểm (một năm học) để so sánh giữa các năm.</summary>
public sealed record GradeBookOverviewDto(
    long GradeBookId,
    string Name,
    int FromYear,
    int ToYear,
    int SubjectCount,
    int EntryCount,
    decimal? OverallAverage10,
    int GoalsDone,
    int GoalsTotal);

/// <summary>Books: năm mới nhất trước. LatestPage: dữ liệu sổ mới nhất (dùng cho mục "cần chú ý" và các popup).</summary>
public sealed record GradesOverviewDto(List<GradeBookOverviewDto> Books, GradesPageDto LatestPage, GradeStatisticsDto LatestStatistics);

// ── Màn "Chi tiết môn" và "Hành trình mục tiêu" ─────────────
/// <summary>
/// Subject: null khi môn không thuộc sổ nào (môn tự do). Ladder: null khi môn chưa có mục tiêu.
/// KnownWeightedSum / KnownWeight: Σ(điểm × hệ số) và Σ hệ số của các điểm đã có.
/// RemainingWeight: tổng hệ số các cột chưa có điểm nào. FocusRequiredScore: điểm tối thiểu ở các cột còn trống
/// để đạt mục tiêu đang theo đuổi (null nếu không còn cột trống, không có mục tiêu, hoặc không thể đạt).
/// </summary>
public sealed record SubjectDetailPageDto(
    GradesPageDto Page,
    SubjectGradeDetailDto? Subject,
    GoalLadderDto? Ladder,
    decimal KnownWeightedSum,
    decimal KnownWeight,
    decimal RemainingWeight,
    GoalDto? FocusGoal,
    decimal? FocusRequiredScore);

// ── Màn "Phân tích học tập" ─────────────────────────────────
public sealed record ScoreBucketDto(string Label, int Count);

public sealed record SubjectStatDto(
    long UserSubjectId,
    string SubjectName,
    string ColorHex,
    decimal? Average10,
    decimal? FocusTarget,
    decimal? AverageChange,
    decimal StudyHours);

/// <summary>Môn nên ưu tiên: mục tiêu đang theo đuổi chưa đạt. RequiredScore: điểm tối thiểu ở các cột còn trống (null: không còn cột trống hoặc không thể đạt).</summary>
public sealed record StudyPriorityDto(
    long UserSubjectId,
    string SubjectName,
    decimal Average10,
    decimal TargetValue,
    decimal Gap,
    bool IsOverdue,
    int? ProgressPercent,
    bool HasEmptyColumns,
    decimal? RequiredScore);

public sealed record GradeStatisticsDto(
    int GoalsDone,
    int GoalsTotal,
    decimal CompletedStudyHours,
    int CompletedSessions,
    int MissedSessions,
    int EntryCount,
    List<ScoreBucketDto> Distribution,
    List<SubjectStatDto> Subjects,
    List<StudyPriorityDto> Priorities);

public sealed record GradeStatisticsPageDto(GradesPageDto Page, GradeStatisticsDto Statistics);
