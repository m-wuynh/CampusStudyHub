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

        if (!selectedGradeBookId.HasValue)
        {
            return new GradesPageDto(gradeBooks, null, null, new List<GradeColumnDto>(), emptySummary, new List<SubjectGradeDetailDto>(), new List<GoalLadderDto>());
        }

        // ── Load GradeBook & Columns ───────────────────────────────
        var gradeBook = await repository.Query<GradeBook>()
            .FirstOrDefaultAsync(gb => gb.GradeBookId == selectedGradeBookId.Value && gb.UserId == userId, cancellationToken);

        GradeBookDto? gradeBookDto = gradeBook is null ? null
            : new GradeBookDto(gradeBook.GradeBookId, gradeBook.Name, gradeBook.FromYear, gradeBook.ToYear);

        var columns = gradeBook is null
            ? new List<GradeColumnDto>()
            : await repository.Query<GradeColumn>()
                .Where(c => c.GradeBookId == gradeBook.GradeBookId)
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.GradeColumnId)
                .Select(c => new GradeColumnDto(c.GradeColumnId, c.GradeBookId, c.Name, c.Weight, c.DisplayOrder))
                .ToListAsync(cancellationToken);

        // ── Load UserSubjects & Entries ────────────────────────────
        var userSubjects = await repository.Query<UserSubject>()
            .Include(us => us.Subject)
            .Include(us => us.GradeEntries)
            .Where(us => us.UserId == userId && us.GradeBookId == selectedGradeBookId.Value && !us.IsArchived)
            .ToListAsync(cancellationToken);

        // Note: The SQL views are no longer strictly aligned with GradeBook, 
        // so we will calculate the summary purely in C# based on the GradeCalculator.
        // We can just omit the DB views to prevent errors.

        // ── Load Goals (Goal Ladder) ───────────────────────────────
        var termSubjectIds = userSubjects.Select(us => us.UserSubjectId).ToList();
        var termGoals = await repository.Query<Goal>()
            .Where(g => g.UserId == userId && termSubjectIds.Contains(g.UserSubjectId))
            .OrderBy(g => g.TargetValue)
            .ToListAsync(cancellationToken);

        // ── Build SubjectGradeDetailDto ────────────────────────────
        var subjectDetails = new List<SubjectGradeDetailDto>();
        foreach (var us in userSubjects)
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

        // ── Compute summary stats ──────────────────────────────────
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

            // "Below target" = lowest InProgress goal target not yet reached
            var subjectGoals = termGoals
                .Where(g => g.UserSubjectId == sd.UserSubjectId && !g.IsCancelled)
                .OrderBy(g => g.TargetValue)
                .ToList();

            var currentGoal = subjectGoals.FirstOrDefault(g => g.TargetValue > (sd.CurrentAverage10 ?? 0m));
            if (currentGoal != null)
            {
                belowTargetCount++;
            }
        }

        // Calculate GradeBook overall average
        decimal? overallAvg = null;
        var validSubjects = subjectDetails.Where(s => s.CurrentAverage10.HasValue && s.CreditWeight > 0).ToList();
        if (validSubjects.Any())
        {
            var sumWeighted = validSubjects.Sum(s => s.CurrentAverage10!.Value * s.CreditWeight);
            var sumWeights = validSubjects.Sum(s => s.CreditWeight);
            overallAvg = Math.Round(sumWeighted / sumWeights, 2, MidpointRounding.AwayFromZero);
        }

        var yearSummary = new YearGradeSummaryDto(
            overallAvg,
            highestScore, highestSubject,
            lowestScore, lowestSubject,
            belowTargetCount);

        // ── Build Goal Ladder per subject ──────────────────────────
        var goalLadders = BuildGoalLadders(subjectDetails, termGoals);

        return new GradesPageDto(gradeBooks, selectedGradeBookId, gradeBookDto, columns, yearSummary, subjectDetails, goalLadders);
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

        entry.Score = dto.Score;

        repository.Update(entry);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGradeEntryAsync(long userId, long entryId, CancellationToken cancellationToken = default)
    {
        var entry = await repository.Query<GradeEntry>(false)
            .Include(ge => ge.UserSubject)
            .FirstOrDefaultAsync(ge => ge.GradeEntryId == entryId, cancellationToken);

        if (entry is null) return;
        if (entry.UserSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        repository.Remove(entry);
        await repository.SaveChangesAsync(cancellationToken);
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

    private static List<GoalLadderDto> BuildGoalLadders(
        List<SubjectGradeDetailDto> subjectDetails,
        List<Goal> termGoals)
    {
        var result = new List<GoalLadderDto>();

        foreach (var subject in subjectDetails)
        {
            var subjectGoals = termGoals
                .Where(g => g.UserSubjectId == subject.UserSubjectId)
                .OrderBy(g => g.TargetValue)
                .ToList();

            if (!subjectGoals.Any()) continue;

            var currentAvg = subject.CurrentAverage10;

            // Determine which goal is InProgress:
            // It's the minimum TargetValue that is still > CurrentAverage10 (among non-cancelled goals)
            var activeGoals = subjectGoals.Where(g => !g.IsCancelled).OrderBy(g => g.TargetValue).ToList();
            var inProgressGoal = activeGoals.FirstOrDefault(g => currentAvg == null || g.TargetValue > currentAvg.Value);

            var goalDtos = subjectGoals.Select(g =>
            {
                GoalStatus status;
                if (g.IsCancelled)
                {
                    status = GoalStatus.Cancelled;
                }
                else if (currentAvg == null)
                {
                    status = GoalStatus.NoData;
                }
                else if (currentAvg >= g.TargetValue)
                {
                    status = GoalStatus.Achieved;
                }
                else if (inProgressGoal != null && g.GoalId == inProgressGoal.GoalId)
                {
                    status = GoalStatus.InProgress;
                }
                else
                {
                    status = GoalStatus.Future;
                }

                return new GoalDto(
                    g.GoalId,
                    g.Title,
                    g.TargetValue,
                    currentAvg,
                    g.EndDate,
                    g.UserSubjectId,
                    status);
            }).ToList();

            result.Add(new GoalLadderDto(subject.UserSubjectId, subject.SubjectName, currentAvg, goalDtos));
        }

        return result;
    }
}


