using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Constants;
using StudyHub.BLL.DTOs;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Grades;

public sealed class GradeService(
    IRepository repository,
    IGradeBookService gradeBookService,
    GoalStatusSyncService goalStatusSync) : IGradeService
{
    public async Task<GradesPageDto> GetGradePageAsync(long userId, long? gradeBookId, CancellationToken cancellationToken = default, bool includeSubjectOptions = true)
    {
        var gradeBooks = await repository.Query<GradeBook>()
            .Where(gb => gb.UserId == userId)
            .OrderByDescending(gb => gb.FromYear)
            .Select(gb => new GradeBookDto(gb.GradeBookId, gb.Name, gb.FromYear, gb.ToYear))
            .ToListAsync(cancellationToken);

        long? selectedGradeBookId = gradeBookId;
        if (selectedGradeBookId == null && gradeBooks.Count > 0)
        {
            selectedGradeBookId = gradeBooks[0].GradeBookId;
        }

        var emptySummary = new YearGradeSummaryDto(null, null, "-", null, "-", 0);

        // ── 1. Load GradeBook & Columns & Table Subjects (CHỈ CHO BẢNG ĐIỂM ĐANG CHỌN) ──
        GradeBookDto? gradeBookDto = null;
        var columns = new List<GradeColumnDto>();
        var subjectDetails = new List<SubjectGradeDetailDto>();
        var yearSummary = emptySummary;

        if (selectedGradeBookId.HasValue)
        {
            var gradeBook = await repository.Query<GradeBook>()
                .FirstOrDefaultAsync(gb => gb.GradeBookId == selectedGradeBookId.Value && gb.UserId == userId, cancellationToken);

            if (gradeBook != null)
            {
                gradeBookDto = new GradeBookDto(gradeBook.GradeBookId, gradeBook.Name, gradeBook.FromYear, gradeBook.ToYear);

                columns = await repository.Query<GradeColumn>()
                    .Where(c => c.GradeBookId == gradeBook.GradeBookId && !c.IsArchived)
                    .OrderBy(c => c.DisplayOrder).ThenBy(c => c.GradeColumnId)
                    .Select(c => new GradeColumnDto(c.GradeColumnId, c.GradeBookId, c.Name, c.Weight, c.DisplayOrder))
                    .ToListAsync(cancellationToken);

                var currentGradeBookSubjects = await repository.Query<UserSubject>()
                    .Include(us => us.Subject)
                    .Include(us => us.GradeEntries.Where(e => !e.IsDeleted))
                    .Where(us => us.UserId == userId && us.GradeBookId == selectedGradeBookId.Value && !us.IsArchived)
                    .OrderBy(us => us.DisplayOrder).ThenBy(us => us.UserSubjectId)
                    .ToListAsync(cancellationToken);

                var columnWeights = columns.ToDictionary(c => c.GradeColumnId, c => c.Weight);

                foreach (var us in currentGradeBookSubjects)
                {
                    var entries = us.GradeEntries
                        .OrderBy(g => g.AssessedOn).ThenBy(g => g.GradeEntryId)
                        .Select(g => new GradeEntryDto(g.GradeEntryId, g.UserSubjectId, g.GradeColumnId, g.Score))
                        .ToList();

                    var avg = GradeCalculator.CalculateWeightedAverage(entries.Select(e => (e.GradeColumnId, e.Score)), columnWeights);

                    subjectDetails.Add(new SubjectGradeDetailDto(
                        us.UserSubjectId,
                        us.CustomSubjectName ?? us.Subject?.SubjectName ?? GradeConstants.FallbackSubjectName,
                        us.ColorHex,
                        us.CreditWeight,
                        avg,
                        entries
                    ));
                }
            }
        }

        // ── 2. Load Goal Sources (TÁCH BIỆT LOGIC BẢNG ĐIỂM VÀ MỤC TIÊU) ──────────
        // Nguồn Goal gồm:
        // a) Các môn thuộc Sổ điểm đang chọn (được tính CurrentAverage10 theo điểm của sổ đó)
        // b) Các môn tự do đã detach (GradeBookId == null) của UserId này (CurrentAverage10 = null vì chưa/không còn điểm)
        string? GetAcademicYearName(long? gbId)
        {
            if (!gbId.HasValue) return null;
            var gb = gradeBooks.FirstOrDefault(b => b.GradeBookId == gbId.Value);
            if (gb == null) return null;
            if (gb.FromYear > 0 && gb.ToYear > 0)
            {
                return $"Năm học {gb.FromYear}–{gb.ToYear}";
            }
            return !string.IsNullOrWhiteSpace(gb.Name) ? gb.Name : null;
        }

        var currentAcademicYearName = GetAcademicYearName(selectedGradeBookId);
        var goalSubjectSources = new List<GoalSubjectSource>();

        foreach (var sd in subjectDetails)
        {
            goalSubjectSources.Add(new GoalSubjectSource(sd.UserSubjectId, sd.SubjectName, sd.CurrentAverage10, selectedGradeBookId, currentAcademicYearName));
        }

        var detachedSubjects = await repository.Query<UserSubject>()
            .Include(us => us.Subject)
            .Where(us => us.UserId == userId && us.GradeBookId == null && !us.IsArchived)
            .ToListAsync(cancellationToken);

        foreach (var ds in detachedSubjects)
        {
            if (!goalSubjectSources.Any(s => s.UserSubjectId == ds.UserSubjectId))
            {
                var name = ds.CustomSubjectName ?? ds.Subject?.SubjectName ?? GradeConstants.FallbackSubjectName;
                goalSubjectSources.Add(new GoalSubjectSource(ds.UserSubjectId, name, null, null, null));
            }
        }

        var allGoalSubjectIds = goalSubjectSources.Select(s => s.UserSubjectId).ToList();
        // Chỉ đọc: trạng thái mục tiêu đã được ghi ở các thao tác POST (GoalStatusSyncService).
        var allGoals = allGoalSubjectIds.Any()
            ? await repository.Query<Goal>()
                .Include(g => g.ScheduleEvents.Where(e => !e.IsDeleted))
                .Include(g => g.GoalHistories)
                .Where(g => g.UserId == userId && allGoalSubjectIds.Contains(g.UserSubjectId) && !g.IsCancelled)
                .OrderBy(g => g.TargetValue)
                .ToListAsync(cancellationToken)
            : new List<Goal>();

        // Hành trình mục tiêu (chỉ đọc): mốc của các môn đang hiển thị + tên các mục tiêu được nhắc tới (kể cả đã hủy).
        var journeyLogs = allGoalSubjectIds.Any()
            ? await repository.Query<GoalProgressLog>()
                .Where(l => l.UserId == userId && allGoalSubjectIds.Contains(l.UserSubjectId))
                .ToListAsync(cancellationToken)
            : new List<GoalProgressLog>();
        var journeyGoalIds = journeyLogs.Where(l => l.GoalId.HasValue).Select(l => l.GoalId!.Value).Distinct().ToList();
        var journeyGoals = journeyGoalIds.Any()
            ? await repository.Query<Goal>()
                .Where(g => g.UserId == userId && journeyGoalIds.Contains(g.GoalId))
                .Select(g => new GoalJourney.GoalInfo(g.GoalId, g.Title, g.TargetValue, g.IsBaseGoal))
                .ToDictionaryAsync(g => g.GoalId, cancellationToken)
            : new Dictionary<long, GoalJourney.GoalInfo>();

        // ── 3. Compute summary stats cho Sổ điểm được chọn ──────────────────────────
        if (subjectDetails.Any())
        {
            decimal? highestScore = null;
            string highestSubject = "-";
            decimal? lowestScore = null;
            string lowestSubject = "-";
            int belowTargetCount = 0;

            foreach (var sd in subjectDetails.Where(s => s.CurrentAverage10.HasValue))
            {
                if (!highestScore.HasValue || sd.CurrentAverage10 > highestScore)
                {
                    highestScore = sd.CurrentAverage10;
                    highestSubject = sd.SubjectName;
                }
                if (!lowestScore.HasValue || sd.CurrentAverage10 < lowestScore)
                {
                    lowestScore = sd.CurrentAverage10;
                    lowestSubject = sd.SubjectName;
                }

                // Q14 = A: môn có điểm và ĐTB hiển thị < mục tiêu gốc (dữ liệu cũ chưa có gốc → mục tiêu tạo sớm nhất).
                var baseGoal = allGoals
                    .Where(g => g.UserSubjectId == sd.UserSubjectId && !g.IsCancelled)
                    .OrderByDescending(g => g.IsBaseGoal)
                    .ThenBy(g => g.CreatedAtUtc).ThenBy(g => g.TargetValue)
                    .FirstOrDefault();
                if (baseGoal != null && GradeCalculator.RoundForDisplay(sd.CurrentAverage10) < baseGoal.TargetValue)
                {
                    belowTargetCount++;
                }
            }

            var overallAvg = GradeCalculator.CalculateOverallAverage(
                subjectDetails.Select(s => (s.CurrentAverage10, s.CreditWeight)));

            yearSummary = new YearGradeSummaryDto(
                overallAvg,
                highestScore, highestSubject,
                lowestScore, lowestSubject,
                belowTargetCount);
        }

        // ── 4. Build Goal Ladder per subject ───────────────────────────────────────
        var goalLadders = BuildGoalLadders(goalSubjectSources, allGoals, journeyLogs, journeyGoals);

        // ── 5. Load All Active Subjects for GradeBook dropdown lookup in Goal modals ──
        var allSubjects = includeSubjectOptions
            ? await repository.Query<UserSubject>()
                .Include(us => us.Subject)
                .Where(us => us.UserId == userId && !us.IsArchived)
                .OrderBy(us => us.CustomSubjectName ?? us.Subject!.SubjectName)
                .Select(us => new GradeSubjectOptionDto(
                    us.UserSubjectId,
                    us.GradeBookId,
                    us.CustomSubjectName ?? us.Subject!.SubjectName ?? GradeConstants.FallbackSubjectName))
                .ToListAsync(cancellationToken)
            : new List<GradeSubjectOptionDto>();

        return new GradesPageDto(gradeBooks, selectedGradeBookId, gradeBookDto, columns, yearSummary, subjectDetails, goalLadders, allSubjects);
    }

    // ── Grade Entry CRUD ─────────────────────────────────────────

    public async Task AddGradeEntryAsync(long userId, CreateGradeEntryDto dto, CancellationToken cancellationToken = default)
    {
        var userSubject = await GetWritableSubjectAsync(userId, dto.UserSubjectId, cancellationToken);

        var column = await repository.Query<GradeColumn>()
            .FirstOrDefaultAsync(c => c.GradeColumnId == dto.GradeColumnId && !c.IsArchived, cancellationToken);
        if (column is null) throw new ArgumentException("Cột đánh giá không tồn tại.");
        if (column.GradeBookId != userSubject.GradeBookId)
            throw new ArgumentException("Cột đánh giá không thuộc sổ điểm của môn học này.");

        GradeGuard.EnsureScore(dto.Score);

        var now = DateTime.UtcNow;
        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        await repository.AddAsync(new GradeEntry
        {
            UserSubjectId = userSubject.UserSubjectId,
            GradeColumnId = column.GradeColumnId,
            Score = dto.Score,
            AssessedOn = GradeClock.Today(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await goalStatusSync.SyncSubjectAsync(userId, userSubject.UserSubjectId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateGradeEntryAsync(long userId, UpdateGradeEntryDto dto, CancellationToken cancellationToken = default)
    {
        var entry = await repository.Query<GradeEntry>(false)
            .FirstOrDefaultAsync(ge => ge.GradeEntryId == dto.Id && !ge.IsDeleted, cancellationToken);
        if (entry is null) throw new ArgumentException("Điểm không tồn tại.");

        await GetWritableSubjectAsync(userId, entry.UserSubjectId, cancellationToken);
        GradeGuard.EnsureScore(dto.Score);

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        entry.Score = dto.Score;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        await goalStatusSync.SyncSubjectAsync(userId, entry.UserSubjectId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteGradeEntryAsync(long userId, long entryId, CancellationToken cancellationToken = default)
    {
        var entry = await repository.Query<GradeEntry>(false)
            .Include(ge => ge.UserSubject)
            .FirstOrDefaultAsync(ge => ge.GradeEntryId == entryId, cancellationToken);

        if (entry is null) return;
        if (entry.UserSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        var userSubjectId = entry.UserSubjectId;
        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        repository.Remove(entry);
        await repository.SaveChangesAsync(cancellationToken);
        await goalStatusSync.SyncSubjectAsync(userId, userSubjectId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Đồng bộ toàn bộ điểm của một môn từ modal nhập điểm: chỉ xét các cột thuộc sổ của môn;
    /// điểm cũ thuộc cột ngoài sổ không bị đụng tới. Dữ liệu không hợp lệ → lỗi, không bỏ qua im lặng.
    /// </summary>
    public async Task SaveSubjectGradesAsync(long userId, SaveSubjectGradesDto dto, CancellationToken cancellationToken = default)
    {
        var subject = await GetWritableSubjectAsync(userId, dto.UserSubjectId, cancellationToken);

        var validColumnIds = (await repository.Query<GradeColumn>()
                .Where(c => c.GradeBookId == subject.GradeBookId && !c.IsArchived)
                .Select(c => c.GradeColumnId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var existingEntries = await repository.Query<GradeEntry>(false)
            .Where(e => e.UserSubjectId == subject.UserSubjectId && !e.IsDeleted && validColumnIds.Contains(e.GradeColumnId))
            .ToListAsync(cancellationToken);
        var existingById = existingEntries.ToDictionary(e => e.GradeEntryId);
        var incoming = dto.Entries ?? new List<GradeEntryInputDto>();

        foreach (var input in incoming)
        {
            if (!validColumnIds.Contains(input.GradeColumnId))
                throw new ArgumentException("Cột đánh giá không thuộc sổ điểm của môn học này.");
            GradeGuard.EnsureScore(input.Score);

            if (input.GradeEntryId is > 0)
            {
                if (!existingById.TryGetValue(input.GradeEntryId.Value, out var existing))
                    throw new ArgumentException("Điểm cần sửa không thuộc môn học này.");
                if (existing.GradeColumnId != input.GradeColumnId)
                    throw new ArgumentException("Không thể chuyển điểm sang cột đánh giá khác.");
            }
        }

        var keptIds = incoming
            .Where(i => i.GradeEntryId is > 0)
            .Select(i => i.GradeEntryId!.Value)
            .ToHashSet();
        var now = DateTime.UtcNow;
        var today = GradeClock.Today();

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);

        repository.RemoveRange(existingEntries.Where(e => !keptIds.Contains(e.GradeEntryId)));
        foreach (var input in incoming)
        {
            if (input.GradeEntryId is > 0)
            {
                var existing = existingById[input.GradeEntryId.Value];
                if (existing.Score != input.Score)
                {
                    existing.Score = input.Score;
                    existing.UpdatedAtUtc = now;
                }
            }
            else
            {
                await repository.AddAsync(new GradeEntry
                {
                    UserSubjectId = subject.UserSubjectId,
                    GradeColumnId = input.GradeColumnId,
                    Score = input.Score,
                    AssessedOn = today,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                }, cancellationToken);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        await goalStatusSync.SyncSubjectAsync(userId, subject.UserSubjectId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Môn của người dùng, chưa lưu trữ và đang thuộc một sổ điểm (Q03: môn không thuộc sổ không được nhập điểm).</summary>
    private async Task<UserSubject> GetWritableSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken)
    {
        var subject = await repository.Query<UserSubject>()
            .FirstOrDefaultAsync(us => us.UserSubjectId == userSubjectId && us.UserId == userId, cancellationToken);
        if (subject is null) throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");
        if (subject.IsArchived) throw new ArgumentException("Không thể nhập điểm cho môn học đã lưu trữ.");
        if (subject.GradeBookId is null) throw new InvalidOperationException("Môn học chưa thuộc sổ điểm nào nên không thể nhập điểm.");
        return subject;
    }

    // ── User Subject CRUD ────────────────────────────────────────

    public async Task CreateUserSubjectAsync(long userId, CreateUserSubjectDto dto, CancellationToken cancellationToken = default)
    {
        var gradeBook = await repository.Query<GradeBook>()
            .FirstOrDefaultAsync(gb => gb.GradeBookId == dto.GradeBookId && gb.UserId == userId, cancellationToken);
        if (gradeBook is null) throw new UnauthorizedAccessException("Không có quyền truy cập sổ điểm này.");

        var name = GradeGuard.RequireText(dto.SubjectName, GradeConstants.SubjectNameMaxLength, "Tên môn học");
        EnsureCreditWeight(dto.CreditWeight);
        await EnsureUniqueSubjectNameAsync(userId, dto.GradeBookId, name, null, cancellationToken);

        var lastOrder = await repository.Query<UserSubject>()
            .Where(us => us.UserId == userId && us.GradeBookId == dto.GradeBookId)
            .MaxAsync(us => (int?)us.DisplayOrder, cancellationToken) ?? 0;

        await repository.AddAsync(new UserSubject
        {
            UserId = userId,
            GradeBookId = dto.GradeBookId,
            SubjectId = null,
            CustomSubjectName = name,
            CreditWeight = dto.CreditWeight,
            ColorHex = GradeConstants.DefaultSubjectColor,
            IsArchived = false,
            DisplayOrder = lastOrder + 1
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserSubjectAsync(long userId, UpdateUserSubjectDto dto, CancellationToken cancellationToken = default)
    {
        var userSubject = await repository.Query<UserSubject>(false)
            .FirstOrDefaultAsync(us => us.UserSubjectId == dto.UserSubjectId, cancellationToken);

        if (userSubject is null) throw new ArgumentException("Môn học không tồn tại.");
        if (userSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        var name = GradeGuard.RequireText(dto.SubjectName, GradeConstants.SubjectNameMaxLength, "Tên môn học");
        EnsureCreditWeight(dto.CreditWeight);
        await EnsureUniqueSubjectNameAsync(userId, userSubject.GradeBookId, name, userSubject.UserSubjectId, cancellationToken);

        userSubject.CustomSubjectName = name;
        userSubject.SubjectId = null;
        userSubject.CreditWeight = dto.CreditWeight;
        if (!string.IsNullOrWhiteSpace(dto.ColorHex))
        {
            if (!ColorHexPattern.IsMatch(dto.ColorHex)) throw new ArgumentException("Màu môn học không hợp lệ (định dạng #RRGGBB).");
            userSubject.ColorHex = dto.ColorHex;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Q09/Q03: xóa cứng môn (kể cả môn không thuộc sổ) và dữ liệu phụ thuộc riêng của môn.</summary>
    public async Task DeleteUserSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default)
    {
        var exists = await repository.AnyAsync<UserSubject>(us => us.UserSubjectId == userSubjectId, cancellationToken);
        if (!exists) return;

        await gradeBookService.DeleteUserSubjectsAsync(userId, new[] { userSubjectId }, cancellationToken);
    }

    private static readonly System.Text.RegularExpressions.Regex ColorHexPattern =
        new("^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static void EnsureCreditWeight(decimal creditWeight)
    {
        if (creditWeight <= 0) throw new ArgumentException("Hệ số môn phải lớn hơn 0.");
    }

    /// <summary>Tên môn duy nhất trong cùng một sổ (không phân biệt hoa thường, đã trim).</summary>
    private async Task EnsureUniqueSubjectNameAsync(long userId, long? gradeBookId, string name, long? exceptSubjectId, CancellationToken cancellationToken)
    {
        var normalized = name.ToLower();
        var duplicate = await repository.AnyAsync<UserSubject>(us =>
            us.UserId == userId &&
            us.GradeBookId == gradeBookId &&
            !us.IsArchived &&
            us.UserSubjectId != exceptSubjectId &&
            us.CustomSubjectName != null &&
            us.CustomSubjectName.Trim().ToLower() == normalized,
            cancellationToken);
        if (duplicate) throw new ArgumentException("Môn học này đã tồn tại trong sổ điểm.");
    }

    public async Task ReorderSubjectsAsync(long userId, List<ReorderSubjectDto> newOrders, CancellationToken cancellationToken = default)
    {
        if (newOrders == null || newOrders.Count == 0) return;

        var subjectIds = newOrders.Select(x => x.UserSubjectId).Distinct().ToList();
        var subjects = await repository.Query<UserSubject>(false)
            .Where(us => us.UserId == userId && subjectIds.Contains(us.UserSubjectId))
            .ToListAsync(cancellationToken);

        if (subjects.Count != subjectIds.Count) throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");
        if (subjects.Select(s => s.GradeBookId).Distinct().Count() != 1)
            throw new ArgumentException("Chỉ sắp xếp được các môn trong cùng một sổ điểm.");

        foreach (var order in newOrders)
        {
            subjects.First(s => s.UserSubjectId == order.UserSubjectId).DisplayOrder = order.NewDisplayOrder;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    // ── Private helpers ──────────────────────────────────────────

    private sealed record GoalSubjectSource(long UserSubjectId, string SubjectName, decimal? CurrentAverage10, long? GradeBookId = null, string? AcademicYearName = null);

    /// <summary>Một ladder cho mỗi môn có mục tiêu, theo đúng UserSubjectId (không gộp môn theo tên).</summary>
    private static List<GoalLadderDto> BuildGoalLadders(
        IEnumerable<GoalSubjectSource> subjects,
        List<Goal> goals,
        List<GoalProgressLog> journeyLogs,
        IReadOnlyDictionary<long, GoalJourney.GoalInfo> journeyGoals)
    {
        var result = new List<GoalLadderDto>();
        var today = GradeClock.Today();

        foreach (var subject in subjects)
        {
            var subjectGoals = goals
                .Where(g => g.UserSubjectId == subject.UserSubjectId && !g.IsCancelled)
                .ToList();
            if (!subjectGoals.Any()) continue;

            var currentAvg = subject.CurrentAverage10;

            // Dữ liệu cũ chưa có mục tiêu gốc: chỉ hiển thị mục tiêu sớm nhất là gốc (không ghi DB trong GET).
            var baseGoalId = subjectGoals
                .Where(g => g.IsBaseGoal)
                .OrderBy(g => g.CreatedAtUtc).ThenBy(g => g.TargetValue)
                .Select(g => (long?)g.GoalId)
                .FirstOrDefault()
                ?? subjectGoals.OrderBy(g => g.CreatedAtUtc).ThenBy(g => g.TargetValue).First().GoalId;

            var goalDtos = new List<GoalDto>();
            foreach (var g in subjectGoals
                         .OrderByDescending(g => g.GoalId == baseGoalId)
                         .ThenBy(g => g.TargetValue))
            {
                var activities = g.ScheduleEvents.Select(e => new GoalActivityDto(
                    e.ScheduleEventId,
                    g.GoalId,
                    e.Title,
                    e.StartAtLocal,
                    e.EndAtLocal,
                    e.Description,
                    e.IsCompleted
                )).OrderBy(a => a.StartAtLocal).ToList();

                var histories = g.GoalHistories.Select(h => new GoalHistoryDto(
                    h.GoalHistoryId,
                    h.GoalId,
                    h.TargetValue,
                    h.AchievedScore,
                    h.AchievedAtUtc,
                    h.CreatedAtUtc
                )).OrderByDescending(h => h.AchievedAtUtc).ToList();

                var lifecycleStatus = GoalStatusEvaluator.Evaluate(currentAvg, g.TargetValue);

                goalDtos.Add(new GoalDto(
                    g.GoalId,
                    g.Title,
                    g.TargetValue,
                    currentAvg,
                    g.EndDate,
                    g.UserSubjectId,
                    GoalStatusEvaluator.ToLegacyStatus(lifecycleStatus),
                    g.GoalId == baseGoalId,
                    lifecycleStatus,
                    g.DisplayOrder,
                    g.AchievedScore,
                    g.AchievedAtUtc,
                    activities,
                    histories,
                    GoalStatusEvaluator.ProgressPercent(currentAvg, g.TargetValue),
                    // Q17: "Quá hạn" chỉ là nhãn, không đổi trạng thái.
                    g.EndDate.HasValue && g.EndDate.Value < today && lifecycleStatus != GradeConstants.GoalLifecycle.Done));
            }

            var baseGoal = goalDtos.FirstOrDefault(g => g.IsBaseGoal);
            var additionalGoals = goalDtos.Where(g => !g.IsBaseGoal).ToList();

            result.Add(new GoalLadderDto(
                subject.UserSubjectId,
                subject.SubjectName,
                currentAvg,
                goalDtos,
                baseGoal != null,
                baseGoal,
                additionalGoals,
                subject.AcademicYearName,
                IsDetached: subject.GradeBookId is null,
                Journey: GoalJourney.Build(journeyLogs.Where(l => l.UserSubjectId == subject.UserSubjectId), journeyGoals)));
        }

        return result.OrderBy(l => l.SubjectName).ToList();
    }
}
