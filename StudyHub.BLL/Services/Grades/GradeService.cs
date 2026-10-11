using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Grades;

public interface IGradeService
{
    Task<GradesPageDto> GetGradePageAsync(long userId, long? gradeBookId, CancellationToken cancellationToken = default);

    // ── Grade Entries ────────────────────────────────────────────
    Task AddGradeEntryAsync(long userId, CreateGradeEntryDto dto, CancellationToken cancellationToken = default);
    Task UpdateGradeEntryAsync(long userId, UpdateGradeEntryDto dto, CancellationToken cancellationToken = default);
    Task DeleteGradeEntryAsync(long userId, long entryId, CancellationToken cancellationToken = default);
    Task SaveSubjectGradesAsync(long userId, SaveSubjectGradesDto dto, CancellationToken cancellationToken = default);

    // ── Academic Term ────────────────────────────────────────────
    Task CreateTermAsync(long userId, CreateTermDto dto, CancellationToken cancellationToken = default);
    Task UpdateTermAsync(long userId, UpdateTermDto dto, CancellationToken cancellationToken = default);
    Task ArchiveTermAsync(long userId, long termId, CancellationToken cancellationToken = default);

    // ── User Subject ─────────────────────────────────────────────
    Task CreateUserSubjectAsync(long userId, CreateUserSubjectDto dto, CancellationToken cancellationToken = default);
    Task UpdateUserSubjectAsync(long userId, UpdateUserSubjectDto dto, CancellationToken cancellationToken = default);
    Task DeleteUserSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default);
    Task ReorderSubjectsAsync(long userId, List<ReorderSubjectDto> newOrders, CancellationToken cancellationToken = default);
}

public sealed class GradeService(IRepository repository) : IGradeService
{
    public async Task<GradesPageDto> GetGradePageAsync(long userId, long? gradeBookId, CancellationToken cancellationToken = default)
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
                    .Where(c => c.GradeBookId == gradeBook.GradeBookId)
                    .OrderBy(c => c.DisplayOrder).ThenBy(c => c.GradeColumnId)
                    .Select(c => new GradeColumnDto(c.GradeColumnId, c.GradeBookId, c.Name, c.Weight, c.DisplayOrder))
                    .ToListAsync(cancellationToken);

                var currentGradeBookSubjects = await repository.Query<UserSubject>()
                    .Include(us => us.Subject)
                    .Include(us => us.GradeEntries)
                    .Where(us => us.UserId == userId && us.GradeBookId == selectedGradeBookId.Value && !us.IsArchived)
                    .ToListAsync(cancellationToken);

                foreach (var us in currentGradeBookSubjects)
                {
                    var entries = us.GradeEntries
                        .Select(g => new GradeEntryDto(g.GradeEntryId, g.UserSubjectId, g.GradeColumnId, g.Score))
                        .ToList();

                    var avg = GradeCalculator.CalculateAverage10FromEntries(entries, columns);

                    subjectDetails.Add(new SubjectGradeDetailDto(
                        us.UserSubjectId,
                        us.CustomSubjectName ?? us.Subject?.SubjectName ?? "Môn học",
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
                var name = ds.CustomSubjectName ?? ds.Subject?.SubjectName ?? "Môn học";
                goalSubjectSources.Add(new GoalSubjectSource(ds.UserSubjectId, name, null, null, null));
            }
        }

        var allGoalSubjectIds = goalSubjectSources.Select(s => s.UserSubjectId).ToList();
        var allGoals = allGoalSubjectIds.Any()
            ? await repository.Query<Goal>(false)
                .Include(g => g.ScheduleEvents.Where(e => !e.IsDeleted))
                .Include(g => g.GoalHistories)
                .Where(g => g.UserId == userId && allGoalSubjectIds.Contains(g.UserSubjectId))
                .OrderBy(g => g.TargetValue)
                .ToListAsync(cancellationToken)
            : new List<Goal>();

        // ── Trạng thái mục tiêu tự động theo điểm số thực tế & Khởi tạo an toàn mục tiêu gốc ──
        bool autoStatusChanged = false;
        var subjectSourcesGrouped = goalSubjectSources
            .GroupBy(s => new { s.GradeBookId, Normalized = s.SubjectName.Trim().ToLowerInvariant() });

        foreach (var group in subjectSourcesGrouped)
        {
            var groupSubjectIds = group.Select(s => s.UserSubjectId).ToList();
            var subGoals = allGoals.Where(g => groupSubjectIds.Contains(g.UserSubjectId) && !g.IsCancelled).ToList();
            if (!subGoals.Any()) continue;

            // Xử lý an toàn mục tiêu gốc:
            // 1) Nếu chưa có mục tiêu gốc: tự động chọn mục tiêu sớm nhất làm mục tiêu gốc
            // 2) Nếu có nhiều mục tiêu gốc: chỉ giữ mục tiêu sớm nhất là gốc
            var baseGoals = subGoals.Where(g => g.IsBaseGoal).OrderBy(g => g.CreatedAtUtc).ThenBy(g => g.TargetValue).ToList();
            if (!baseGoals.Any())
            {
                var earliest = subGoals.OrderBy(g => g.CreatedAtUtc).ThenBy(g => g.TargetValue).First();
                earliest.IsBaseGoal = true;
                earliest.UpdatedAtUtc = DateTime.UtcNow;
                repository.Update(earliest);
                autoStatusChanged = true;
            }
            else if (baseGoals.Count > 1)
            {
                for (int i = 1; i < baseGoals.Count; i++)
                {
                    baseGoals[i].IsBaseGoal = false;
                    baseGoals[i].UpdatedAtUtc = DateTime.UtcNow;
                    repository.Update(baseGoals[i]);
                    autoStatusChanged = true;
                }
            }

            var canonicalAvg = group.FirstOrDefault(s => s.CurrentAverage10.HasValue)?.CurrentAverage10;

            if (canonicalAvg.HasValue)
            {
                var curAvg = canonicalAvg.Value;
                foreach (var g in subGoals)
                {
                    if (curAvg >= g.TargetValue)
                    {
                        if (g.Status != "Done")
                        {
                            g.Status = "Done";
                            if (!g.AchievedScore.HasValue)
                            {
                                g.AchievedScore = curAvg;
                                g.AchievedAtUtc = DateTime.UtcNow;
                            }
                            g.UpdatedAtUtc = DateTime.UtcNow;
                            repository.Update(g);
                            autoStatusChanged = true;
                        }
                    }
                    else
                    {
                        // Đã có điểm thực tế nhưng chưa đạt TargetValue => InProgress
                        if (g.Status != "InProgress" && !g.AchievedScore.HasValue)
                        {
                            g.Status = "InProgress";
                            g.UpdatedAtUtc = DateTime.UtcNow;
                            repository.Update(g);
                            autoStatusChanged = true;
                        }
                    }
                }
            }
            else
            {
                // Không có điểm thực tế (ví dụ chưa nhập điểm hoặc vừa xóa bảng điểm)
                foreach (var g in subGoals)
                {
                    // Bảo toàn trạng thái Done và thành tích nếu đã từng đạt trước đó
                    if (g.AchievedScore.HasValue || g.Status == "Done")
                    {
                        continue;
                    }

                    // Chưa từng đạt => ToDo
                    if (g.Status != "ToDo")
                    {
                        g.Status = "ToDo";
                        g.UpdatedAtUtc = DateTime.UtcNow;
                        repository.Update(g);
                        autoStatusChanged = true;
                    }
                }
            }
        }
        if (autoStatusChanged)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

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

                var subjectGoals = allGoals
                    .Where(g => g.UserSubjectId == sd.UserSubjectId && !g.IsCancelled)
                    .OrderBy(g => g.TargetValue)
                    .ToList();

                var currentGoal = subjectGoals.FirstOrDefault(g => g.TargetValue > (sd.CurrentAverage10 ?? 0m));
                if (currentGoal != null)
                {
                    belowTargetCount++;
                }
            }

            decimal? overallAvg = null;
            var validSubjects = subjectDetails.Where(s => s.CurrentAverage10.HasValue && s.CreditWeight > 0).ToList();
            if (validSubjects.Any())
            {
                var sumWeighted = validSubjects.Sum(s => s.CurrentAverage10!.Value * s.CreditWeight);
                var sumWeights = validSubjects.Sum(s => s.CreditWeight);
                overallAvg = Math.Round(sumWeighted / sumWeights, 2, MidpointRounding.AwayFromZero);
            }

            yearSummary = new YearGradeSummaryDto(
                overallAvg,
                highestScore, highestSubject,
                lowestScore, lowestSubject,
                belowTargetCount);
        }

        // ── 4. Build Goal Ladder per subject ───────────────────────────────────────
        var goalLadders = BuildGoalLadders(goalSubjectSources, allGoals);

        // ── 5. Load All Active Subjects for GradeBook dropdown lookup in Goal modals ──
        var allSubjects = await repository.Query<UserSubject>()
            .Include(us => us.Subject)
            .Where(us => us.UserId == userId && !us.IsArchived)
            .OrderBy(us => us.CustomSubjectName ?? us.Subject!.SubjectName)
            .Select(us => new GradeSubjectOptionDto(
                us.UserSubjectId,
                us.GradeBookId,
                us.CustomSubjectName ?? us.Subject!.SubjectName ?? "Môn học"))
            .ToListAsync(cancellationToken);

        return new GradesPageDto(gradeBooks, selectedGradeBookId, gradeBookDto, columns, yearSummary, subjectDetails, goalLadders, allSubjects);
    }

    // ── Grade Entry CRUD ─────────────────────────────────────────

    public async Task AddGradeEntryAsync(long userId, CreateGradeEntryDto dto, CancellationToken cancellationToken = default)
    {
        // Verify subject access
        var userSubject = await repository.Query<UserSubject>()
            .FirstOrDefaultAsync(us => us.UserSubjectId == dto.UserSubjectId && us.UserId == userId, cancellationToken);
        if (userSubject is null) throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");
        if (userSubject.IsArchived) throw new ArgumentException("Không thể nhập điểm cho môn học đã lưu trữ.");

        // Verify column access
        var column = await repository.Query<GradeColumn>()
            .Include(c => c.GradeBook)
            .FirstOrDefaultAsync(c => c.GradeColumnId == dto.GradeColumnId, cancellationToken);
        if (column is null) throw new ArgumentException("Cột đánh giá không tồn tại.");
        if (column.GradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập cột này.");

        // Validate score 0–10
        if (dto.Score < 0 || dto.Score > 10) throw new ArgumentException("Điểm phải nằm trong khoảng 0 đến 10.");

        var entry = new GradeEntry
        {
            UserSubjectId = dto.UserSubjectId,
            GradeColumnId = dto.GradeColumnId,
            Score = dto.Score
        };

        await repository.AddAsync(entry, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await SyncGoalStatusesForSubjectAsync(userId, dto.UserSubjectId, cancellationToken);
    }

    public async Task UpdateGradeEntryAsync(long userId, UpdateGradeEntryDto dto, CancellationToken cancellationToken = default)
    {
        var entry = await repository.Query<GradeEntry>(false)
            .Include(ge => ge.UserSubject)
            .Include(ge => ge.GradeColumn)
            .FirstOrDefaultAsync(ge => ge.GradeEntryId == dto.Id, cancellationToken);

        if (entry is null) throw new ArgumentException("Điểm không tồn tại.");
        if (entry.UserSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        if (dto.Score < 0 || dto.Score > 10) throw new ArgumentException("Điểm phải nằm trong khoảng 0 đến 10.");

        var userSubjectId = entry.UserSubjectId;
        entry.Score = dto.Score;

        repository.Update(entry);
        await repository.SaveChangesAsync(cancellationToken);
        await SyncGoalStatusesForSubjectAsync(userId, userSubjectId, cancellationToken);
    }

    public async Task DeleteGradeEntryAsync(long userId, long entryId, CancellationToken cancellationToken = default)
    {
        var entry = await repository.Query<GradeEntry>(false)
            .Include(ge => ge.UserSubject)
            .FirstOrDefaultAsync(ge => ge.GradeEntryId == entryId, cancellationToken);

        if (entry is null) return;
        if (entry.UserSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        var userSubjectId = entry.UserSubjectId;
        repository.Remove(entry);
        await repository.SaveChangesAsync(cancellationToken);
        await SyncGoalStatusesForSubjectAsync(userId, userSubjectId, cancellationToken);
    }

    public async Task SaveSubjectGradesAsync(long userId, SaveSubjectGradesDto dto, CancellationToken cancellationToken = default)
    {
        var subject = await repository.Query<UserSubject>()
            .Include(us => us.GradeEntries)
            .FirstOrDefaultAsync(us => us.UserSubjectId == dto.UserSubjectId && us.UserId == userId && !us.IsArchived, cancellationToken);

        if (subject is null) throw new ArgumentException("Môn học không tồn tại.");

        var validColumnIds = await repository.Query<GradeColumn>()
            .Where(c => c.GradeBookId == subject.GradeBookId)
            .Select(c => c.GradeColumnId)
            .ToListAsync(cancellationToken);

        var existingEntries = subject.GradeEntries.ToList();
        var incomingEntries = dto.Entries ?? new List<GradeEntryInputDto>();

        // 1. Delete entries missing in incoming list
        var incomingEntryIds = incomingEntries.Where(e => e.GradeEntryId.HasValue && e.GradeEntryId.Value > 0).Select(e => e.GradeEntryId!.Value).ToHashSet();
        foreach (var existing in existingEntries)
        {
            if (!incomingEntryIds.Contains(existing.GradeEntryId))
            {
                repository.Remove(existing);
            }
        }

        // 2. Add / Update incoming entries
        foreach (var entryDto in incomingEntries)
        {
            if (!validColumnIds.Contains(entryDto.GradeColumnId)) continue;
            if (entryDto.Score < 0 || entryDto.Score > 10) throw new ArgumentException("Điểm số phải từ 0 đến 10.");

            if (entryDto.GradeEntryId.HasValue && entryDto.GradeEntryId.Value > 0)
            {
                var existing = existingEntries.FirstOrDefault(e => e.GradeEntryId == entryDto.GradeEntryId.Value);
                if (existing != null)
                {
                    existing.Score = entryDto.Score;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                    repository.Update(existing);
                }
            }
            else
            {
                var newEntry = new GradeEntry
                {
                    UserSubjectId = dto.UserSubjectId,
                    GradeColumnId = entryDto.GradeColumnId,
                    Score = entryDto.Score,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await repository.AddAsync(newEntry, cancellationToken);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        await SyncGoalStatusesForSubjectAsync(userId, dto.UserSubjectId, cancellationToken);
    }

    // ── Academic Term CRUD ───────────────────────────────────────

    public async Task CreateTermAsync(long userId, CreateTermDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.TermName)) throw new ArgumentException("Tên học kỳ không được để trống.");
        if (dto.StartDate >= dto.EndDate) throw new ArgumentException("Ngày bắt đầu phải trước ngày kết thúc.");
        if (dto.EndDate < dto.StartDate.AddMonths(3)) throw new ArgumentException("Học kỳ phải kéo dài tối thiểu 3 tháng.");
        if (dto.EndDate > dto.StartDate.AddMonths(6)) throw new ArgumentException("Học kỳ không được vượt quá 6 tháng.");

        var existingName = await repository.AnyAsync<AcademicTerm>(t => t.UserId == userId && t.TermName == dto.TermName, cancellationToken);
        if (existingName) throw new ArgumentException("Tên học kỳ đã tồn tại.");

        var overlapping = await repository.AnyAsync<AcademicTerm>(t =>
            t.UserId == userId && !t.IsArchived &&
            t.StartDate <= dto.EndDate && t.EndDate >= dto.StartDate,
            cancellationToken);
        if (overlapping) throw new ArgumentException("Thời gian học kỳ bị chồng lấn với một học kỳ khác.");

        var term = new AcademicTerm
        {
            UserId = userId,
            TermName = dto.TermName,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsArchived = false
        };

        await repository.AddAsync(term, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTermAsync(long userId, UpdateTermDto dto, CancellationToken cancellationToken = default)
    {
        var term = await repository.Query<AcademicTerm>(false)
            .FirstOrDefaultAsync(t => t.AcademicTermId == dto.Id, cancellationToken);

        if (term is null) throw new ArgumentException("Học kỳ không tồn tại.");
        if (term.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        if (string.IsNullOrWhiteSpace(dto.TermName)) throw new ArgumentException("Tên học kỳ không được để trống.");
        if (dto.StartDate >= dto.EndDate) throw new ArgumentException("Ngày bắt đầu phải trước ngày kết thúc.");
        if (dto.EndDate < dto.StartDate.AddMonths(3)) throw new ArgumentException("Học kỳ phải kéo dài tối thiểu 3 tháng.");
        if (dto.EndDate > dto.StartDate.AddMonths(6)) throw new ArgumentException("Học kỳ không được vượt quá 6 tháng.");

        var existingName = await repository.AnyAsync<AcademicTerm>(t => t.UserId == userId && t.TermName == dto.TermName && t.AcademicTermId != dto.Id, cancellationToken);
        if (existingName) throw new ArgumentException("Tên học kỳ đã tồn tại.");

        var overlapping = await repository.AnyAsync<AcademicTerm>(t =>
            t.UserId == userId && t.AcademicTermId != dto.Id && !t.IsArchived &&
            t.StartDate <= dto.EndDate && t.EndDate >= dto.StartDate,
            cancellationToken);
        if (overlapping) throw new ArgumentException("Thời gian học kỳ bị chồng lấn với một học kỳ khác.");

        term.TermName = dto.TermName;
        term.StartDate = dto.StartDate;
        term.EndDate = dto.EndDate;

        repository.Update(term);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveTermAsync(long userId, long termId, CancellationToken cancellationToken = default)
    {
        var term = await repository.Query<AcademicTerm>(false)
            .FirstOrDefaultAsync(t => t.AcademicTermId == termId, cancellationToken);

        if (term is null) return;
        if (term.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        term.IsArchived = true;
        repository.Update(term);
        await repository.SaveChangesAsync(cancellationToken);
    }

    // ── User Subject CRUD ────────────────────────────────────────

    public async Task CreateUserSubjectAsync(long userId, CreateUserSubjectDto dto, CancellationToken cancellationToken = default)
    {
        var gradeBook = await repository.Query<GradeBook>()
            .FirstOrDefaultAsync(gb => gb.GradeBookId == dto.GradeBookId && gb.UserId == userId, cancellationToken);
        if (gradeBook is null) throw new UnauthorizedAccessException("Không có quyền truy cập sổ điểm này.");

        if (string.IsNullOrWhiteSpace(dto.SubjectName)) throw new ArgumentException("Tên môn học không được để trống.");

        var existing = await repository.AnyAsync<UserSubject>(
            us => us.UserId == userId && us.GradeBookId == dto.GradeBookId && us.CustomSubjectName == dto.SubjectName.Trim() && !us.IsArchived,
            cancellationToken);
        if (existing) throw new ArgumentException("Môn học này đã tồn tại trong sổ điểm.");

        var userSubject = new UserSubject
        {
            UserId = userId,
            GradeBookId = dto.GradeBookId,
            SubjectId = null,
            CustomSubjectName = dto.SubjectName.Trim(),
            CreditWeight = dto.CreditWeight,
            ColorHex = "#5138EE",
            IsArchived = false
        };

        await repository.AddAsync(userSubject, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserSubjectAsync(long userId, UpdateUserSubjectDto dto, CancellationToken cancellationToken = default)
    {
        var userSubject = await repository.Query<UserSubject>(false)
            .FirstOrDefaultAsync(us => us.UserSubjectId == dto.UserSubjectId, cancellationToken);

        if (userSubject is null) throw new ArgumentException("Môn học không tồn tại.");
        if (userSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        if (string.IsNullOrWhiteSpace(dto.SubjectName)) throw new ArgumentException("Tên môn học không được để trống.");

        userSubject.CustomSubjectName = dto.SubjectName.Trim();
        userSubject.CreditWeight = dto.CreditWeight;
        userSubject.ColorHex = dto.ColorHex ?? "#5138EE";

        repository.Update(userSubject);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUserSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default)
    {
        var userSubject = await repository.Query<UserSubject>(false)
            .FirstOrDefaultAsync(us => us.UserSubjectId == userSubjectId, cancellationToken);

        if (userSubject is null) return;
        if (userSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        // Check if ANY Goal exists (active or cancelled)
        var hasAnyGoal = await repository.AnyAsync<Goal>(
            g => g.UserSubjectId == userSubjectId, cancellationToken);
        if (hasAnyGoal)
            throw new InvalidOperationException("Không thể xóa môn này vì môn này đang có mục tiêu học tập.");

        var entries = await repository.Query<GradeEntry>(false)
            .Where(ge => ge.UserSubjectId == userSubjectId)
            .ToListAsync(cancellationToken);
        foreach (var e in entries) repository.Remove(e);

        repository.Remove(userSubject);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderSubjectsAsync(long userId, List<ReorderSubjectDto> newOrders, CancellationToken cancellationToken = default)
    {
        var subjectIds = newOrders.Select(x => x.UserSubjectId).ToList();
        var subjects = await repository.Query<UserSubject>(false)
            .Where(us => us.UserId == userId && subjectIds.Contains(us.UserSubjectId))
            .ToListAsync(cancellationToken);

        // Optional: you can store DisplayOrder in UserSubject if you want to remember order.
        // But looking at the entity UserSubject, there is NO DisplayOrder column.
        // Wait, did I miss this? Let's check UserSubject.cs first.
    }

    // ── Private helpers ──────────────────────────────────────────

    private sealed record GoalSubjectSource(long UserSubjectId, string SubjectName, decimal? CurrentAverage10, long? GradeBookId = null, string? AcademicYearName = null);

    private static List<GoalLadderDto> BuildGoalLadders(
        IEnumerable<GoalSubjectSource> subjects,
        List<Goal> goals)
    {
        var result = new List<GoalLadderDto>();

        // Nhóm các môn theo GradeBookId và tên môn học chuẩn hóa (không phân biệt hoa thường)
        // để mọi mục tiêu của cùng một môn trong cùng bảng điểm năm học luôn gộp chung vào 1 Goal Ladder
        var subjectGroups = subjects
            .GroupBy(s => new
            {
                s.GradeBookId,
                NormalizedName = (s.SubjectName ?? string.Empty).Trim().ToLowerInvariant()
            })
            .ToList();

        foreach (var group in subjectGroups)
        {
            var groupSubjectIds = group.Select(s => s.UserSubjectId).ToHashSet();
            var subjectGoals = goals
                .Where(g => groupSubjectIds.Contains(g.UserSubjectId) && !g.IsCancelled)
                .ToList();

            if (!subjectGoals.Any()) continue;

            // Lấy môn học đại diện có điểm trung bình thực tế nếu có
            var canonicalSubject = group.FirstOrDefault(s => s.CurrentAverage10.HasValue) ?? group.First();
            var currentAvg = canonicalSubject.CurrentAverage10;
            var academicYearName = canonicalSubject.AcademicYearName ?? group.FirstOrDefault(s => !string.IsNullOrEmpty(s.AcademicYearName))?.AcademicYearName;

            // Xử lý an toàn dữ liệu cũ: nếu môn đã có mục tiêu nhưng chưa có IsBaseGoal, mục tiêu sớm nhất tự động là mục tiêu gốc
            var baseGoals = subjectGoals.Where(g => g.IsBaseGoal).OrderBy(g => g.CreatedAtUtc).ThenBy(g => g.TargetValue).ToList();
            if (!baseGoals.Any() && subjectGoals.Any())
            {
                var earliest = subjectGoals.OrderBy(g => g.CreatedAtUtc).ThenBy(g => g.TargetValue).First();
                earliest.IsBaseGoal = true;
            }

            var goalDtos = new List<GoalDto>();
            // Mục tiêu gốc xếp đầu tiên, tiếp theo là các nấc bổ sung theo DisplayOrder rồi đến TargetValue
            foreach (var g in subjectGoals.OrderByDescending(g => g.IsBaseGoal).ThenBy(g => g.DisplayOrder).ThenBy(g => g.TargetValue))
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

                // Xác định trạng thái tự động theo điểm số thực tế
                string lifecycleStatus;
                if (currentAvg.HasValue)
                {
                    if (currentAvg.Value >= g.TargetValue)
                    {
                        lifecycleStatus = "Done";
                    }
                    else
                    {
                        lifecycleStatus = "InProgress";
                    }
                }
                else
                {
                    // Không có điểm thực tế (ví dụ chưa nhập điểm hoặc đã xóa sổ điểm)
                    if (g.AchievedScore.HasValue || g.Status == "Done")
                    {
                        lifecycleStatus = "Done";
                    }
                    else
                    {
                        lifecycleStatus = "ToDo";
                    }
                }

                GoalStatus legacyStatus;
                if (lifecycleStatus == "Done") legacyStatus = GoalStatus.Achieved;
                else if (lifecycleStatus == "InProgress") legacyStatus = GoalStatus.InProgress;
                else legacyStatus = GoalStatus.NoData;

                goalDtos.Add(new GoalDto(
                    g.GoalId,
                    g.Title,
                    g.TargetValue,
                    currentAvg,
                    g.EndDate,
                    g.UserSubjectId,
                    legacyStatus,
                    g.IsBaseGoal,
                    lifecycleStatus,
                    g.DisplayOrder,
                    g.AchievedScore,
                    g.AchievedAtUtc,
                    activities,
                    histories));
            }

            var baseGoal = goalDtos.FirstOrDefault(g => g.IsBaseGoal);
            var additionalGoals = goalDtos.Where(g => !g.IsBaseGoal).ToList();

            result.Add(new GoalLadderDto(
                canonicalSubject.UserSubjectId,
                canonicalSubject.SubjectName,
                currentAvg,
                goalDtos,
                baseGoal != null,
                baseGoal,
                additionalGoals,
                academicYearName));
        }

        return result.OrderBy(l => l.SubjectName).ToList();
    }

    private async Task SyncGoalStatusesForSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken)
    {
        var subject = await repository.Query<UserSubject>()
            .Include(us => us.GradeEntries)
            .FirstOrDefaultAsync(us => us.UserSubjectId == userSubjectId && us.UserId == userId && !us.IsArchived, cancellationToken);

        if (subject == null) return;

        var goals = await repository.Query<Goal>(false)
            .Where(g => g.UserId == userId && g.UserSubjectId == userSubjectId && !g.IsCancelled)
            .ToListAsync(cancellationToken);

        if (!goals.Any()) return;

        decimal? curAvg = null;
        if (subject.GradeBookId.HasValue)
        {
            var columns = await repository.Query<GradeColumn>()
                .Where(c => c.GradeBookId == subject.GradeBookId.Value)
                .ToListAsync(cancellationToken);

            curAvg = GradeCalculator.CalculateSubjectAverage(subject.GradeEntries, columns);
        }

        bool changed = false;
        if (curAvg.HasValue)
        {
            var avg = curAvg.Value;
            foreach (var g in goals)
            {
                if (avg >= g.TargetValue)
                {
                    if (g.Status != "Done")
                    {
                        g.Status = "Done";
                        if (!g.AchievedScore.HasValue)
                        {
                            g.AchievedScore = avg;
                            g.AchievedAtUtc = DateTime.UtcNow;
                        }
                        g.UpdatedAtUtc = DateTime.UtcNow;
                        repository.Update(g);
                        changed = true;
                    }
                }
                else
                {
                    if (g.Status != "InProgress" && !g.AchievedScore.HasValue)
                    {
                        g.Status = "InProgress";
                        g.UpdatedAtUtc = DateTime.UtcNow;
                        repository.Update(g);
                        changed = true;
                    }
                }
            }
        }
        else
        {
            foreach (var g in goals)
            {
                if (!g.AchievedScore.HasValue && g.Status != "Done")
                {
                    if (g.Status != "ToDo")
                    {
                        g.Status = "ToDo";
                        g.UpdatedAtUtc = DateTime.UtcNow;
                        repository.Update(g);
                        changed = true;
                    }
                }
            }
        }

        if (changed)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}


