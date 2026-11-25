using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Grades;

public interface IGradeService
{
    Task<GradesPageDto> GetGradePageAsync(long userId, long? gradeBookId, CancellationToken cancellationToken = default, bool includeSubjectOptions = true);

    // ── Grade Entries ────────────────────────────────────────────
    Task AddGradeEntryAsync(long userId, CreateGradeEntryDto dto, CancellationToken cancellationToken = default);
    Task UpdateGradeEntryAsync(long userId, UpdateGradeEntryDto dto, CancellationToken cancellationToken = default);
    Task DeleteGradeEntryAsync(long userId, long entryId, CancellationToken cancellationToken = default);
    Task SaveSubjectGradesAsync(long userId, SaveSubjectGradesDto dto, CancellationToken cancellationToken = default);

    // ── User Subject ─────────────────────────────────────────────
    Task CreateUserSubjectAsync(long userId, CreateUserSubjectDto dto, CancellationToken cancellationToken = default);
    Task UpdateUserSubjectAsync(long userId, UpdateUserSubjectDto dto, CancellationToken cancellationToken = default);
    Task DeleteUserSubjectAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default);
    Task ReorderSubjectsAsync(long userId, List<ReorderSubjectDto> newOrders, CancellationToken cancellationToken = default);
}

