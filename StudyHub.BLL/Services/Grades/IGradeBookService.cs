using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Grades;

public interface IGradeBookService
{
    Task<GradeBookDto> CreateGradeBookWithColumnsAsync(long userId, CreateGradeBookWithColumnsDto dto, CancellationToken cancellationToken = default);

    Task UpdateGradeBookAsync(long userId, UpdateGradeBookDto dto, CancellationToken cancellationToken = default);

    Task DeleteGradeBookAsync(long userId, long gradeBookId, CancellationToken cancellationToken = default);

    // ── Column operations ────────────────────────────────────────
    Task<GradeColumnDto> AddColumnAsync(long userId, CreateGradeColumnDto dto, CancellationToken cancellationToken = default);

    Task UpdateColumnAsync(long userId, UpdateGradeColumnDto dto, CancellationToken cancellationToken = default);

    Task DeleteColumnAsync(long userId, long gradeColumnId, CancellationToken cancellationToken = default);

    Task ReorderColumnsAsync(long userId, long gradeBookId, List<ReorderColumnDto> newOrders, CancellationToken cancellationToken = default);
}

