using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Grades;

public interface IGradeService
{
    Task<GradesPageDto> GetGradePageAsync(long userId, long? gradeBookId, CancellationToken cancellationToken = default, bool includeSubjectOptions = true);

    // ── Các màn chỉ đọc ──────────────────────────────────────────
    /// <summary>Màn "Tổng quan năm học": tóm tắt mọi sổ điểm, năm mới nhất trước.</summary>
    Task<GradesOverviewDto> GetGradesOverviewAsync(long userId, CancellationToken cancellationToken = default);
    /// <summary>Màn "Chi tiết môn" / "Hành trình mục tiêu". Null khi môn không tồn tại, đã lưu trữ hoặc không thuộc người dùng.</summary>
    Task<SubjectDetailPageDto?> GetSubjectDetailAsync(long userId, long userSubjectId, CancellationToken cancellationToken = default);
    /// <summary>Màn "Phân tích học tập" của một sổ điểm (null: sổ mới nhất).</summary>
    Task<GradeStatisticsPageDto> GetStatisticsAsync(long userId, long? gradeBookId, CancellationToken cancellationToken = default);

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

