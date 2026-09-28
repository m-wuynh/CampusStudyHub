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
    Task<GradesPageDto> GetGradePageAsync(long userId, long? termId, CancellationToken cancellationToken = default);
    Task AddGradeEntryAsync(long userId, CreateGradeEntryDto dto, CancellationToken cancellationToken = default);
    Task UpdateGradeEntryAsync(long userId, UpdateGradeEntryDto dto, CancellationToken cancellationToken = default);
    Task DeleteGradeEntryAsync(long userId, long entryId, CancellationToken cancellationToken = default);

    Task CreateTermAsync(long userId, CreateTermDto dto, CancellationToken cancellationToken = default);
    Task UpdateTermAsync(long userId, UpdateTermDto dto, CancellationToken cancellationToken = default);
    Task ArchiveTermAsync(long userId, long termId, CancellationToken cancellationToken = default);

    Task CreateUserSubjectAsync(long userId, CreateUserSubjectDto dto, CancellationToken cancellationToken = default);
    Task UpdateUserSubjectAsync(long userId, UpdateUserSubjectDto dto, CancellationToken cancellationToken = default);
    Task ArchiveUserSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default);
}

public sealed class GradeService(IRepository repository) : IGradeService
{
    public async Task<GradesPageDto> GetGradePageAsync(long userId, long? termId, CancellationToken cancellationToken = default)
    {
        var terms = await repository.Query<AcademicTerm>()
            .Where(t => t.UserId == userId && !t.IsArchived)
            .OrderByDescending(t => t.StartDate)
            .Select(t => new AcademicTermDto(t.AcademicTermId, t.TermName, t.StartDate, t.EndDate))
            .ToListAsync(cancellationToken);

        long? selectedTermId = termId;
        if (selectedTermId == null && terms.Count > 0)
        {
            selectedTermId = terms[0].Id;
        }

        var summary = new TermGradeSummaryDto(null, null, "-", null, "-", 0);
        var subjectDetails = new List<SubjectGradeDetailDto>();

        if (selectedTermId.HasValue)
        {
            var userSubjects = await repository.Query<UserSubject>()
                .Include(us => us.Subject)
                .Include(us => us.GradeEntries.Where(ge => !ge.IsDeleted))
                .Where(us => us.UserId == userId && us.AcademicTermId == selectedTermId.Value && !us.IsArchived)
                .ToListAsync(cancellationToken);

            var vwSummaries = await repository.Query<VwSubjectGradeSummary>()
                .Where(v => v.UserId == userId && v.AcademicTermId == selectedTermId.Value)
                .ToListAsync(cancellationToken);

            var vwTermSummary = await repository.Query<VwTermGradeSummary>()
                .FirstOrDefaultAsync(v => v.UserId == userId && v.AcademicTermId == selectedTermId.Value, cancellationToken);

            var termSubjectIds = userSubjects.Select(us => us.UserSubjectId).ToList();
            var termGoals = await repository.Query<Goal>()
                .Where(g => g.UserId == userId && !g.IsCancelled && g.UserSubjectId != null && termSubjectIds.Contains(g.UserSubjectId.Value))
                .ToListAsync(cancellationToken);

            foreach (var us in userSubjects)
            {
                var vwSummary = vwSummaries.FirstOrDefault(v => v.UserSubjectId == us.UserSubjectId);
                var entries = us.GradeEntries.Select(g => new GradeEntryDto(
                    g.GradeEntryId, g.UserSubjectId, g.Title, g.AssessmentType, g.Score, g.MaxScore, g.Weight, g.AssessedOn
                )).OrderBy(g => g.AssessedOn).ToList();

                var avg = vwSummary?.CurrentAverage10 ?? GradeCalculator.CalculateAverage10(entries);

                subjectDetails.Add(new SubjectGradeDetailDto(
                    us.UserSubjectId,
                    us.CustomSubjectName ?? us.Subject?.SubjectName ?? "Môn học",
                    us.ColorHex,
                    us.TargetScore10,
                    us.CreditWeight,
                    avg,
                    entries
                ));
            }

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

                var subjectGoal = termGoals
                    .Where(g => g.UserSubjectId == sd.UserSubjectId)
                    .OrderByDescending(g => g.CreatedAtUtc)
                    .FirstOrDefault();

                if (subjectGoal != null && sd.CurrentAverage10 < subjectGoal.TargetValue)
                {
                    belowTargetCount++;
                }
            }

            summary = new TermGradeSummaryDto(
                vwTermSummary?.CurrentAverage10,
                highestScore,
                highestSubject,
                lowestScore,
                lowestSubject,
                belowTargetCount
            );
        }

        return new GradesPageDto(terms, selectedTermId, summary, subjectDetails, new List<GoalDto>());
    }

    public async Task AddGradeEntryAsync(long userId, CreateGradeEntryDto dto, CancellationToken cancellationToken = default)
    {
        var hasAccess = await repository.AnyAsync<UserSubject>(us => us.UserSubjectId == dto.UserSubjectId && us.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");

        var entry = new GradeEntry
        {
            UserSubjectId = dto.UserSubjectId,
            Title = dto.Title,
            AssessmentType = dto.AssessmentType,
            Score = dto.Score,
            MaxScore = dto.MaxScore,
            Weight = dto.Weight,
            AssessedOn = dto.AssessedOn,
            IsDeleted = false
        };

        await repository.AddAsync(entry, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGradeEntryAsync(long userId, UpdateGradeEntryDto dto, CancellationToken cancellationToken = default)
    {
        var entry = await repository.Query<GradeEntry>(false)
            .Include(ge => ge.UserSubject)
            .FirstOrDefaultAsync(ge => ge.GradeEntryId == dto.Id, cancellationToken);

        if (entry == null) throw new ArgumentException("Điểm không tồn tại.");
        if (entry.UserSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        entry.Title = dto.Title;
        entry.AssessmentType = dto.AssessmentType;
        entry.Score = dto.Score;
        entry.MaxScore = dto.MaxScore;
        entry.Weight = dto.Weight;
        entry.AssessedOn = dto.AssessedOn;

        repository.Update(entry);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGradeEntryAsync(long userId, long entryId, CancellationToken cancellationToken = default)
    {
        var entry = await repository.Query<GradeEntry>(false)
            .Include(ge => ge.UserSubject)
            .FirstOrDefaultAsync(ge => ge.GradeEntryId == entryId, cancellationToken);

        if (entry == null) return;
        if (entry.UserSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        entry.IsDeleted = true;
        repository.Update(entry);
        await repository.SaveChangesAsync(cancellationToken);
    }

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

        if (term == null) throw new ArgumentException("Học kỳ không tồn tại.");
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

        if (term == null) return;
        if (term.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        term.IsArchived = true;
        repository.Update(term);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateUserSubjectAsync(long userId, CreateUserSubjectDto dto, CancellationToken cancellationToken = default)
    {
        var hasTermAccess = await repository.AnyAsync<AcademicTerm>(t => t.AcademicTermId == dto.AcademicTermId && t.UserId == userId, cancellationToken);
        if (!hasTermAccess) throw new UnauthorizedAccessException("Không có quyền truy cập học kỳ này.");

        var existing = await repository.AnyAsync<UserSubject>(us => us.UserId == userId && us.AcademicTermId == dto.AcademicTermId && us.CustomSubjectName == dto.SubjectName && !us.IsArchived, cancellationToken);
        if (existing) throw new ArgumentException("Môn học này đã tồn tại trong học kỳ.");

        var userSubject = new UserSubject
        {
            UserId = userId,
            AcademicTermId = dto.AcademicTermId,
            SubjectId = null,
            CustomSubjectName = dto.SubjectName,
            TargetScore10 = null,
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

        if (userSubject == null) throw new ArgumentException("Môn học không tồn tại.");
        if (userSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        userSubject.CustomSubjectName = dto.SubjectName;
        userSubject.TargetScore10 = dto.TargetScore10;
        userSubject.CreditWeight = dto.CreditWeight;
        userSubject.ColorHex = dto.ColorHex ?? "#5138EE";

        repository.Update(userSubject);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveUserSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default)
    {
        var userSubject = await repository.Query<UserSubject>(false)
            .FirstOrDefaultAsync(us => us.UserSubjectId == userSubjectId, cancellationToken);

        if (userSubject == null) return;
        if (userSubject.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        userSubject.IsArchived = true;
        repository.Update(userSubject);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
