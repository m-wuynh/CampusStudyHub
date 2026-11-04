using System;

namespace StudyHub.BLL.Constants;

/// <summary>
/// Giá trị nghiệp vụ dùng chung của Grades & Goals (thang điểm, độ dài khớp cấu hình DB, giá trị mặc định).
/// </summary>
public static class GradeConstants
{
    // ── Thang điểm 10 ────────────────────────────────────────────
    public const decimal ScoreMin = 0m;
    public const decimal ScoreMax = 10m;

    /// <summary>Mục tiêu hợp lệ trong khoảng (0, 10].</summary>
    public const decimal TargetMax = 10m;

    /// <summary>Số chữ số thập phân khi lưu/tính ĐTB.</summary>
    public const int AverageDecimals = 2;

    /// <summary>Số chữ số thập phân khi hiển thị ĐTB.</summary>
    public const int DisplayDecimals = 1;

    /// <summary>Khoảng "gần đạt" mục tiêu (tô màu vàng trên bảng điểm).</summary>
    public const decimal NearTargetMargin = 0.5m;

    // ── Độ dài tối đa (khớp HasMaxLength trong StudyHubDbContext) ─
    public const int GradeBookNameMaxLength = 200;
    public const int ColumnNameMaxLength = 100;
    public const int SubjectNameMaxLength = 150;
    public const int GoalTitleMaxLength = 200;
    public const int ActivityTitleMaxLength = 200;
    public const int ActivityDescriptionMaxLength = 2000;

    // ── Giá trị mặc định ─────────────────────────────────────────
    public const string DefaultSubjectColor = "#5138EE";
    public const string FallbackSubjectName = "Môn học";
    public const string DefaultTimeZoneId = "SE Asia Standard Time";

    // ── Goal Activity (ScheduleEvent) ────────────────────────────
    public const string GoalActivityEventType = "SelfStudy";
    public const string GoalActivityRepeatMode = "None";

    /// <summary>Giá trị cột <c>Goal.Status</c>.</summary>
    public static class GoalLifecycle
    {
        public const string ToDo = "ToDo";
        public const string InProgress = "InProgress";
        public const string Done = "Done";
    }
}

/// <summary>Kiểm tra đầu vào dùng chung cho Grades & Goals.</summary>
public static class GradeGuard
{
    public static void EnsureScore(decimal score)
    {
        if (score < GradeConstants.ScoreMin || score > GradeConstants.ScoreMax)
            throw new ArgumentException($"Điểm phải nằm trong khoảng {GradeConstants.ScoreMin:0} đến {GradeConstants.ScoreMax:0}.");
    }

    public static void EnsureColumnWeight(decimal weight)
    {
        if (weight <= 0) throw new ArgumentException("Hệ số cột phải lớn hơn 0.");
    }

    public static void EnsureTarget(decimal target)
    {
        if (target <= GradeConstants.ScoreMin || target > GradeConstants.TargetMax)
            throw new ArgumentException($"Điểm mục tiêu phải nằm trong khoảng ({GradeConstants.ScoreMin:0}, {GradeConstants.TargetMax:0}].");
    }

    /// <summary>Trim và kiểm tra bắt buộc + độ dài tối đa; trả về chuỗi đã trim.</summary>
    public static string RequireText(string? value, int maxLength, string fieldLabel)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException($"{fieldLabel} không được để trống.");
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"{fieldLabel} không được vượt quá {maxLength} ký tự.");
        return trimmed;
    }
}

/// <summary>Ngày hiện tại theo múi giờ mặc định của ứng dụng (dùng cho <c>GradeEntry.AssessedOn</c>).</summary>
public static class GradeClock
{
    private static readonly TimeZoneInfo AppTimeZone = ResolveTimeZone();

    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AppTimeZone));

    /// <summary>Giờ hiển thị theo múi giờ ứng dụng (không phụ thuộc múi giờ của máy chủ).</summary>
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), AppTimeZone);

    private static TimeZoneInfo ResolveTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(GradeConstants.DefaultTimeZoneId); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}
