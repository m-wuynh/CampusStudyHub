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

public interface IGradeBookService
{
    /// <summary>
    /// Gets the GradeBook for (userId, fromYear, toYear). Returns null if none exists yet.
    /// </summary>
    Task<GradeBookDto?> GetGradeBookAsync(long userId, int fromYear, int toYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a GradeBook for (userId, year). Throws if one already exists.
    /// </summary>
    Task<GradeBookDto> CreateGradeBookAsync(long userId, CreateGradeBookDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the name of an existing GradeBook.
    /// </summary>
    Task<GradeBookDto> CreateGradeBookWithColumnsAsync(long userId, CreateGradeBookWithColumnsDto dto, CancellationToken cancellationToken = default);

    Task UpdateGradeBookAsync(long userId, UpdateGradeBookDto dto, CancellationToken cancellationToken = default);

    Task DeleteGradeBookAsync(long userId, long gradeBookId, CancellationToken cancellationToken = default);

    // ── Column operations ────────────────────────────────────────
    Task<List<GradeColumnDto>> GetColumnsAsync(long userId, long gradeBookId, CancellationToken cancellationToken = default);

    Task<GradeColumnDto> AddColumnAsync(long userId, CreateGradeColumnDto dto, CancellationToken cancellationToken = default);

    Task UpdateColumnAsync(long userId, UpdateGradeColumnDto dto, CancellationToken cancellationToken = default);

    Task DeleteColumnAsync(long userId, long gradeColumnId, CancellationToken cancellationToken = default);

    Task ReorderColumnsAsync(long userId, long gradeBookId, List<ReorderColumnDto> newOrders, CancellationToken cancellationToken = default);
}

public sealed class GradeBookService(IRepository repository) : IGradeBookService
{
    // ── GradeBook ────────────────────────────────────────────────

    public async Task<GradeBookDto?> GetGradeBookAsync(long userId, int fromYear, int toYear, CancellationToken cancellationToken = default)
    {
        var gradeBook = await repository.Query<GradeBook>()
            .FirstOrDefaultAsync(gb => gb.UserId == userId && gb.FromYear == fromYear && gb.ToYear == toYear, cancellationToken);

        return gradeBook is null ? null : new GradeBookDto(gradeBook.GradeBookId, gradeBook.Name, gradeBook.FromYear, gradeBook.ToYear);
    }

    public async Task<GradeBookDto> CreateGradeBookAsync(long userId, CreateGradeBookDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.FromYear >= dto.ToYear) throw new ArgumentException("Năm kết thúc phải lớn hơn năm bắt đầu.");

        var alreadyExists = await repository.AnyAsync<GradeBook>(
            gb => gb.UserId == userId && gb.FromYear == dto.FromYear && gb.ToYear == dto.ToYear, cancellationToken);
        if (alreadyExists) throw new InvalidOperationException("Sổ điểm này đã tồn tại");

        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Tên sổ điểm không được để trống.");
        if (dto.Name.Length > 200) throw new ArgumentException("Tên sổ điểm không được vượt quá 200 ký tự.");

        var gradeBook = new GradeBook
        {
            UserId = userId,
            FromYear = dto.FromYear,
            ToYear = dto.ToYear,
            Name = dto.Name.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(gradeBook, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new GradeBookDto(gradeBook.GradeBookId, gradeBook.Name, gradeBook.FromYear, gradeBook.ToYear);
    }

    public async Task UpdateGradeBookAsync(long userId, UpdateGradeBookDto dto, CancellationToken cancellationToken = default)
    {
        var gradeBook = await repository.Query<GradeBook>(false)
            .FirstOrDefaultAsync(gb => gb.GradeBookId == dto.GradeBookId, cancellationToken);

        if (gradeBook is null) throw new ArgumentException("Bảng điểm không tồn tại.");
        if (gradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Tên bảng điểm không được để trống.");
        if (dto.Name.Length > 200) throw new ArgumentException("Tên bảng điểm không được vượt quá 200 ký tự.");

        gradeBook.Name = dto.Name.Trim();
        gradeBook.UpdatedAtUtc = DateTime.UtcNow;
        repository.Update(gradeBook);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGradeBookAsync(long userId, long gradeBookId, CancellationToken cancellationToken = default)
    {
        if (gradeBookId <= 0) throw new ArgumentException("ID sổ điểm không hợp lệ.");

        var gradeBook = await repository.Query<GradeBook>(false)
            .FirstOrDefaultAsync(gb => gb.GradeBookId == gradeBookId, cancellationToken);

        if (gradeBook is null) throw new ArgumentException("Sổ điểm không tồn tại hoặc đã bị xóa.");
        if (gradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Lấy tất cả UserSubject thuộc GradeBook để GỠ LIÊN KẾT (detach), KHÔNG XÓA UserSubject & Goal
            var subjects = await repository.Query<UserSubject>(false)
                .Where(us => us.GradeBookId == gradeBookId)
                .ToListAsync(cancellationToken);

            var subjectIds = subjects.Select(us => us.UserSubjectId).ToList();

            // 2. Lấy tất cả GradeColumn của GradeBook
            var columns = await repository.Query<GradeColumn>(false)
                .Where(c => c.GradeBookId == gradeBookId)
                .ToListAsync(cancellationToken);

            var columnIds = columns.Select(c => c.GradeColumnId).ToList();

            // 3. Xóa các GradeEntry liên quan (thuộc cột điểm hoặc môn học của sổ điểm này)
            if (columnIds.Any() || subjectIds.Any())
            {
                var entries = await repository.Query<GradeEntry>(false)
                    .Where(ge => columnIds.Contains(ge.GradeColumnId) || (ge.UserSubjectId != 0 && subjectIds.Contains(ge.UserSubjectId)))
                    .ToListAsync(cancellationToken);
                foreach (var e in entries) repository.Remove(e);
            }

            // 4. Detach UserSubject (Gán GradeBookId = null để giữ nguyên UserSubject và Goal)
            // Lấy danh sách các UserSubject hiện đã unlinked (GradeBookId == null) để tránh vi phạm unique index UQ_UserSubjects_CustomEnrollment
            var existingUnlinkedSubjects = await repository.Query<UserSubject>(false)
                .Where(us => us.UserId == userId && us.GradeBookId == null && !us.IsArchived)
                .ToListAsync(cancellationToken);

            foreach (var s in subjects)
            {
                var targetUnlinked = existingUnlinkedSubjects.FirstOrDefault(u =>
                    (s.SubjectId.HasValue && u.SubjectId == s.SubjectId) ||
                    (!string.IsNullOrWhiteSpace(s.CustomSubjectName) && string.Equals(u.CustomSubjectName, s.CustomSubjectName, StringComparison.OrdinalIgnoreCase)));

                if (targetUnlinked != null && targetUnlinked.UserSubjectId != s.UserSubjectId)
                {
                    // Đã có môn unlinked cùng tên => Chuyển các Goal & tính năng khác sang môn unlinked có sẵn, rồi xóa s
                    var goalsToMove = await repository.Query<Goal>(false)
                        .Where(g => g.UserSubjectId == s.UserSubjectId)
                        .ToListAsync(cancellationToken);

                    foreach (var g in goalsToMove)
                    {
                        var hasDupGoal = await repository.AnyAsync<Goal>(
                            x => x.UserId == userId && x.UserSubjectId == targetUnlinked.UserSubjectId && x.TargetValue == g.TargetValue && !x.IsCancelled,
                            cancellationToken);
                        if (!hasDupGoal)
                        {
                            g.UserSubjectId = targetUnlinked.UserSubjectId;
                            repository.Update(g);
                        }
                        else
                        {
                            repository.Remove(g);
                        }
                    }

                    var deadlines = await repository.Query<Deadline>(false).Where(d => d.UserSubjectId == s.UserSubjectId).ToListAsync(cancellationToken);
                    foreach (var d in deadlines) { d.UserSubjectId = targetUnlinked.UserSubjectId; repository.Update(d); }

                    var decks = await repository.Query<FlashcardDeck>(false).Where(fd => fd.UserSubjectId == s.UserSubjectId).ToListAsync(cancellationToken);
                    foreach (var fd in decks) { fd.UserSubjectId = targetUnlinked.UserSubjectId; repository.Update(fd); }

                    var notes = await repository.Query<Note>(false).Where(n => n.UserSubjectId == s.UserSubjectId).ToListAsync(cancellationToken);
                    foreach (var n in notes) { n.UserSubjectId = targetUnlinked.UserSubjectId; repository.Update(n); }

                    var events = await repository.Query<ScheduleEvent>(false).Where(se => se.UserSubjectId == s.UserSubjectId).ToListAsync(cancellationToken);
                    foreach (var se in events) { se.UserSubjectId = targetUnlinked.UserSubjectId; repository.Update(se); }

                    var sessions = await repository.Query<StudySession>(false).Where(ss => ss.UserSubjectId == s.UserSubjectId).ToListAsync(cancellationToken);
                    foreach (var ss in sessions) { ss.UserSubjectId = targetUnlinked.UserSubjectId; repository.Update(ss); }

                    var docs = await repository.Query<Document>(false).Where(doc => doc.UserSubjectId == s.UserSubjectId).ToListAsync(cancellationToken);
                    foreach (var doc in docs) { doc.UserSubjectId = targetUnlinked.UserSubjectId; repository.Update(doc); }

                    repository.Remove(s);
                }
                else
                {
                    s.GradeBookId = null;
                    repository.Update(s);
                    existingUnlinkedSubjects.Add(s);
                }
            }

            // 5. Xóa GradeColumn
            foreach (var c in columns) repository.Remove(c);

            // 6. Xóa GradeBook
            repository.Remove(gradeBook);

            // 7. Commit transaction
            await repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<GradeBookDto> CreateGradeBookWithColumnsAsync(long userId, CreateGradeBookWithColumnsDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.FromYear >= dto.ToYear) throw new ArgumentException("Năm kết thúc phải lớn hơn năm bắt đầu.");

        var alreadyExists = await repository.AnyAsync<GradeBook>(
            gb => gb.UserId == userId && gb.FromYear == dto.FromYear && gb.ToYear == dto.ToYear, cancellationToken);
        if (alreadyExists) throw new InvalidOperationException("Sổ điểm này đã tồn tại");

        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Tên bảng điểm không được để trống.");

        var gradeBook = new GradeBook
        {
            UserId = userId,
            FromYear = dto.FromYear,
            ToYear = dto.ToYear,
            Name = dto.Name.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(gradeBook, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // Add Columns
        if (dto.Columns != null && dto.Columns.Any())
        {
            int order = 1;
            foreach (var col in dto.Columns)
            {
                if (string.IsNullOrWhiteSpace(col.Name) || col.Weight <= 0) continue;
                await repository.AddAsync(new GradeColumn
                {
                    GradeBookId = gradeBook.GradeBookId,
                    Name = col.Name.Trim(),
                    Weight = col.Weight,
                    DisplayOrder = order++,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        // Add or Reuse Subjects
        if (dto.SubjectNames != null && dto.SubjectNames.Any())
        {
            var cleanNames = dto.SubjectNames
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleanNames.Any())
            {
                var existingSubjects = await repository.Query<UserSubject>(false)
                    .Where(us => us.UserId == userId && us.CustomSubjectName != null && cleanNames.Contains(us.CustomSubjectName))
                    .ToListAsync(cancellationToken);

                foreach (var subj in cleanNames)
                {
                    var existing = existingSubjects.FirstOrDefault(us => string.Equals(us.CustomSubjectName, subj, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.GradeBookId = gradeBook.GradeBookId;
                        repository.Update(existing);
                    }
                    else
                    {
                        await repository.AddAsync(new UserSubject
                        {
                            UserId = userId,
                            GradeBookId = gradeBook.GradeBookId,
                            CustomSubjectName = subj,
                            CreditWeight = 1,
                            ColorHex = "#4f46e5"
                        }, cancellationToken);
                    }
                }
            }
        }

        await repository.SaveChangesAsync(cancellationToken);

        return new GradeBookDto(gradeBook.GradeBookId, gradeBook.Name, gradeBook.FromYear, gradeBook.ToYear);
    }

    // ── GradeColumn ──────────────────────────────────────────────

    public async Task<List<GradeColumnDto>> GetColumnsAsync(long userId, long gradeBookId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await repository.AnyAsync<GradeBook>(gb => gb.GradeBookId == gradeBookId && gb.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException("Không có quyền truy cập bảng điểm này.");

        return await repository.Query<GradeColumn>()
            .Where(c => c.GradeBookId == gradeBookId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.GradeColumnId)
            .Select(c => new GradeColumnDto(c.GradeColumnId, c.GradeBookId, c.Name, c.Weight, c.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<GradeColumnDto> AddColumnAsync(long userId, CreateGradeColumnDto dto, CancellationToken cancellationToken = default)
    {
        var gradeBook = await repository.Query<GradeBook>()
            .FirstOrDefaultAsync(gb => gb.GradeBookId == dto.GradeBookId, cancellationToken);

        if (gradeBook is null) throw new ArgumentException("Bảng điểm không tồn tại.");
        if (gradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập bảng điểm này.");

        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Tên cột không được để trống.");
        if (dto.Name.Length > 100) throw new ArgumentException("Tên cột không được vượt quá 100 ký tự.");
        if (dto.Weight <= 0) throw new ArgumentException("Hệ số cột phải lớn hơn 0.");

        var nameExists = await repository.AnyAsync<GradeColumn>(
            c => c.GradeBookId == dto.GradeBookId && c.Name == dto.Name.Trim(),
            cancellationToken);
        if (nameExists) throw new ArgumentException($"Cột '{dto.Name}' đã tồn tại trong bảng điểm này.");

        var column = new GradeColumn
        {
            GradeBookId = dto.GradeBookId,
            Name = dto.Name.Trim(),
            Weight = dto.Weight,
            DisplayOrder = dto.DisplayOrder,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(column, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new GradeColumnDto(column.GradeColumnId, column.GradeBookId, column.Name, column.Weight, column.DisplayOrder);
    }

    public async Task UpdateColumnAsync(long userId, UpdateGradeColumnDto dto, CancellationToken cancellationToken = default)
    {
        var column = await repository.Query<GradeColumn>(false)
            .Include(c => c.GradeBook)
            .FirstOrDefaultAsync(c => c.GradeColumnId == dto.GradeColumnId, cancellationToken);

        if (column is null) throw new ArgumentException("Cột không tồn tại.");
        if (column.GradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Tên cột không được để trống.");
        if (dto.Name.Length > 100) throw new ArgumentException("Tên cột không được vượt quá 100 ký tự.");
        if (dto.Weight <= 0) throw new ArgumentException("Hệ số cột phải lớn hơn 0.");

        var nameExists = await repository.AnyAsync<GradeColumn>(
            c => c.GradeBookId == column.GradeBookId && c.Name == dto.Name.Trim() && c.GradeColumnId != dto.GradeColumnId,
            cancellationToken);
        if (nameExists) throw new ArgumentException($"Cột '{dto.Name}' đã tồn tại trong bảng điểm này.");

        column.Name = dto.Name.Trim();
        column.Weight = dto.Weight;
        column.DisplayOrder = dto.DisplayOrder;
        column.UpdatedAtUtc = DateTime.UtcNow;

        repository.Update(column);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteColumnAsync(long userId, long gradeColumnId, CancellationToken cancellationToken = default)
    {
        var column = await repository.Query<GradeColumn>(false)
            .Include(c => c.GradeBook)
            .FirstOrDefaultAsync(c => c.GradeColumnId == gradeColumnId, cancellationToken);

        if (column is null) return;
        if (column.GradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        var entries = await repository.Query<GradeEntry>(false)
            .Where(ge => ge.GradeColumnId == gradeColumnId)
            .ToListAsync(cancellationToken);
        foreach (var e in entries) repository.Remove(e);

        repository.Remove(column);

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderColumnsAsync(long userId, long gradeBookId, List<ReorderColumnDto> newOrders, CancellationToken cancellationToken = default)
    {
        var hasAccess = await repository.AnyAsync<GradeBook>(gb => gb.GradeBookId == gradeBookId && gb.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException("Không có quyền truy cập bảng điểm này.");

        var columnIds = newOrders.Select(x => x.GradeColumnId).ToList();
        var columns = await repository.Query<GradeColumn>(false)
            .Where(c => c.GradeBookId == gradeBookId && columnIds.Contains(c.GradeColumnId))
            .ToListAsync(cancellationToken);

        foreach (var col in columns)
        {
            var order = newOrders.FirstOrDefault(x => x.GradeColumnId == col.GradeColumnId);
            if (order != null)
            {
                col.DisplayOrder = order.NewDisplayOrder;
                repository.Update(col);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}
