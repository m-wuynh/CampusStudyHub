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

public sealed class GradeBookService(
    IRepository repository,
    GoalStatusSyncService goalStatusSync) : IGradeBookService
{
    // ── GradeBook ────────────────────────────────────────────────

    public async Task UpdateGradeBookAsync(long userId, UpdateGradeBookDto dto, CancellationToken cancellationToken = default)
    {
        var gradeBook = await repository.Query<GradeBook>(false)
            .FirstOrDefaultAsync(gb => gb.GradeBookId == dto.GradeBookId, cancellationToken);

        if (gradeBook is null) throw new ArgumentException("Bảng điểm không tồn tại.");
        if (gradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        gradeBook.Name = GradeGuard.RequireText(dto.Name, GradeConstants.GradeBookNameMaxLength, "Tên sổ điểm");
        gradeBook.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<GradeBookDto> CreateGradeBookWithColumnsAsync(long userId, CreateGradeBookWithColumnsDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.FromYear >= dto.ToYear) throw new ArgumentException("Năm kết thúc phải lớn hơn năm bắt đầu.");
        var name = GradeGuard.RequireText(dto.Name, GradeConstants.GradeBookNameMaxLength, "Tên sổ điểm");

        var columns = (dto.Columns ?? new List<CreateColumnInitDto>())
            .Select(c => (Name: GradeGuard.RequireText(c.Name, GradeConstants.ColumnNameMaxLength, "Tên cột"), c.Weight))
            .ToList();
        foreach (var column in columns) GradeGuard.EnsureColumnWeight(column.Weight);
        if (columns.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new ArgumentException("Tên cột bị trùng trong sổ điểm.");

        // Mỗi sổ điểm có bộ UserSubject riêng: luôn tạo mới, không tái sử dụng môn của sổ khác hay môn không thuộc sổ.
        var subjectNames = (dto.SubjectNames ?? new List<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => GradeGuard.RequireText(n, GradeConstants.SubjectNameMaxLength, "Tên môn học"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var alreadyExists = await repository.AnyAsync<GradeBook>(
            gb => gb.UserId == userId && gb.FromYear == dto.FromYear && gb.ToYear == dto.ToYear, cancellationToken);
        if (alreadyExists) throw new InvalidOperationException("Sổ điểm này đã tồn tại");

        var now = DateTime.UtcNow;
        var gradeBook = new GradeBook
        {
            UserId = userId,
            FromYear = dto.FromYear,
            ToYear = dto.ToYear,
            Name = name,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var order = 1;
        foreach (var column in columns)
        {
            gradeBook.GradeColumns.Add(new GradeColumn
            {
                Name = column.Name,
                Weight = column.Weight,
                DisplayOrder = order++,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        var subjectOrder = 1;
        foreach (var subjectName in subjectNames)
        {
            gradeBook.UserSubjects.Add(new UserSubject
            {
                UserId = userId,
                CustomSubjectName = subjectName,
                CreditWeight = 1,
                ColorHex = GradeConstants.DefaultSubjectColor,
                DisplayOrder = subjectOrder++
            });
        }

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        await repository.AddAsync(gradeBook, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GradeBookDto(gradeBook.GradeBookId, gradeBook.Name, gradeBook.FromYear, gradeBook.ToYear);
    }

    // ── GradeColumn ──────────────────────────────────────────────

    public async Task<GradeColumnDto> AddColumnAsync(long userId, CreateGradeColumnDto dto, CancellationToken cancellationToken = default)
    {
        var gradeBook = await repository.Query<GradeBook>()
            .FirstOrDefaultAsync(gb => gb.GradeBookId == dto.GradeBookId, cancellationToken);

        if (gradeBook is null) throw new ArgumentException("Bảng điểm không tồn tại.");
        if (gradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập bảng điểm này.");

        var name = GradeGuard.RequireText(dto.Name, GradeConstants.ColumnNameMaxLength, "Tên cột");
        GradeGuard.EnsureColumnWeight(dto.Weight);

        var nameExists = await repository.AnyAsync<GradeColumn>(
            c => c.GradeBookId == dto.GradeBookId && c.Name == name,
            cancellationToken);
        if (nameExists) throw new ArgumentException($"Cột '{name}' đã tồn tại trong bảng điểm này.");

        var column = new GradeColumn
        {
            GradeBookId = dto.GradeBookId,
            Name = name,
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

        var name = GradeGuard.RequireText(dto.Name, GradeConstants.ColumnNameMaxLength, "Tên cột");
        GradeGuard.EnsureColumnWeight(dto.Weight);

        var nameExists = await repository.AnyAsync<GradeColumn>(
            c => c.GradeBookId == column.GradeBookId && c.Name == name && c.GradeColumnId != dto.GradeColumnId,
            cancellationToken);
        if (nameExists) throw new ArgumentException($"Cột '{name}' đã tồn tại trong bảng điểm này.");

        var weightChanged = column.Weight != dto.Weight;
        column.Name = name;
        column.Weight = dto.Weight;
        column.DisplayOrder = dto.DisplayOrder;
        column.UpdatedAtUtc = DateTime.UtcNow;

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        // Đổi hệ số cột làm đổi ĐTB mọi môn của sổ → đồng bộ trạng thái mục tiêu ngay trong POST.
        if (weightChanged) await goalStatusSync.SyncGradeBookAsync(userId, column.GradeBookId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        var gradeBookId = column.GradeBookId;
        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        repository.RemoveRange(entries);
        repository.Remove(column);

        await repository.SaveChangesAsync(cancellationToken);
        await goalStatusSync.SyncGradeBookAsync(userId, gradeBookId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    // ── Xóa sổ điểm / môn học (trước đây là GradeBookDeletionService) ─────────
    // Xóa cứng GradeBook / UserSubject theo nghiệp vụ chính thức (Master Plan §2.4, Q09, Q25):
    // - Xóa: GradeBook, GradeColumn, UserSubject, GradeEntry, Goal, GoalHistory, GoalProgressLog (hành trình), ScheduleEvent của môn/mục tiêu
    // (kèm Reminder, ScheduleException), Deadline (kèm Reminder), StudySession.
    // - Giữ nội dung, chỉ gỡ liên kết (UserSubjectId = NULL): Note, Document, FlashcardDeck.
    // Toàn bộ trong một transaction; kiểm tra dữ liệu chéo sổ trước khi xóa và kiểm tra mồ côi sau khi xóa.

    public async Task DeleteGradeBookAsync(long userId, long gradeBookId, CancellationToken cancellationToken = default)
    {
        if (gradeBookId <= 0) throw new ArgumentException("ID sổ điểm không hợp lệ.");

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);

        var gradeBook = await repository.Query<GradeBook>(false)
            .FirstOrDefaultAsync(gb => gb.GradeBookId == gradeBookId, cancellationToken)
            ?? throw new ArgumentException("Sổ điểm không tồn tại hoặc đã bị xóa.");
        if (gradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        var subjectIds = await repository.Query<UserSubject>()
            .Where(us => us.UserId == userId && us.GradeBookId == gradeBookId)
            .Select(us => us.UserSubjectId)
            .ToListAsync(cancellationToken);
        var columns = await repository.Query<GradeColumn>(false)
            .Where(c => c.GradeBookId == gradeBookId)
            .ToListAsync(cancellationToken);
        var columnIds = columns.Select(c => c.GradeColumnId).ToList();

        // Chỉ xóa dữ liệu của đúng sổ này: dừng nếu có điểm nối giữa sổ này và sổ khác.
        var hasCrossGradeBookEntries = await repository.Query<GradeEntry>().AnyAsync(e =>
            (subjectIds.Contains(e.UserSubjectId) && !columnIds.Contains(e.GradeColumnId)) ||
            (columnIds.Contains(e.GradeColumnId) && !subjectIds.Contains(e.UserSubjectId)), cancellationToken);
        if (hasCrossGradeBookEntries)
            throw new InvalidOperationException("Dữ liệu điểm của sổ này đang liên kết với sổ khác nên chưa thể xóa. Vui lòng liên hệ quản trị để kiểm tra dữ liệu.");

        var scope = await StageSubjectDataRemovalAsync(userId, subjectIds, cancellationToken);
        repository.RemoveRange(columns);
        repository.Remove(gradeBook);

        await repository.SaveChangesAsync(cancellationToken);
        await EnsureNoOrphansAsync(scope, columnIds, gradeBookId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Xóa cứng một hoặc nhiều môn (kể cả môn không thuộc sổ) và dữ liệu phụ thuộc riêng của môn.</summary>
    public async Task DeleteUserSubjectsAsync(long userId, IReadOnlyCollection<long> userSubjectIds, CancellationToken cancellationToken = default)
    {
        if (userSubjectIds.Count == 0) return;

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);

        var ownedIds = await repository.Query<UserSubject>()
            .Where(us => us.UserId == userId && userSubjectIds.Contains(us.UserSubjectId))
            .Select(us => us.UserSubjectId)
            .ToListAsync(cancellationToken);
        if (ownedIds.Count != userSubjectIds.Distinct().Count())
            throw new UnauthorizedAccessException("Không có quyền truy cập môn học này.");

        var scope = await StageSubjectDataRemovalAsync(userId, ownedIds, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
        await EnsureNoOrphansAsync(scope, new List<long>(), null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record DeletionScope(
        List<long> SubjectIds,
        List<long> GoalIds,
        List<long> ScheduleEventIds,
        List<long> DeadlineIds);

    /// <summary>Đánh dấu xóa/gỡ liên kết toàn bộ dữ liệu phụ thuộc của các môn (chưa SaveChanges).</summary>
    private async Task<DeletionScope> StageSubjectDataRemovalAsync(long userId, List<long> subjectIds, CancellationToken ct)
    {
        var goals = await repository.Query<Goal>(false)
            .Where(g => g.UserId == userId && subjectIds.Contains(g.UserSubjectId))
            .ToListAsync(ct);
        var goalIds = goals.Select(g => g.GoalId).ToList();

        // Lịch của mục tiêu và lịch gắn môn (kể cả bản ghi đã xóa mềm, đã hoàn thành, đã gỡ GoalId trước đây).
        var events = await repository.Query<ScheduleEvent>(false)
            .Where(e => e.UserId == userId &&
                        ((e.GoalId != null && goalIds.Contains(e.GoalId.Value)) ||
                         (e.UserSubjectId != null && subjectIds.Contains(e.UserSubjectId.Value))))
            .ToListAsync(ct);
        var eventIds = events.Select(e => e.ScheduleEventId).ToList();

        var deadlines = await repository.Query<Deadline>(false)
            .Where(d => d.UserId == userId && d.UserSubjectId != null && subjectIds.Contains(d.UserSubjectId.Value))
            .ToListAsync(ct);
        var deadlineIds = deadlines.Select(d => d.DeadlineId).ToList();

        repository.RemoveRange(await repository.Query<Reminder>(false)
            .Where(r => r.UserId == userId &&
                        ((r.ScheduleEventId != null && eventIds.Contains(r.ScheduleEventId.Value)) ||
                         (r.DeadlineId != null && deadlineIds.Contains(r.DeadlineId.Value))))
            .ToListAsync(ct));
        repository.RemoveRange(await repository.Query<ScheduleException>(false)
            .Where(x => eventIds.Contains(x.ScheduleEventId))
            .ToListAsync(ct));
        repository.RemoveRange(events);
        repository.RemoveRange(await repository.Query<GoalHistory>(false)
            .Where(h => goalIds.Contains(h.GoalId))
            .ToListAsync(ct));
        repository.RemoveRange(await repository.Query<GoalProgressLog>(false)
            .Where(l => subjectIds.Contains(l.UserSubjectId) || (l.GoalId != null && goalIds.Contains(l.GoalId.Value)))
            .ToListAsync(ct));
        repository.RemoveRange(goals);
        repository.RemoveRange(await repository.Query<GradeEntry>(false)
            .Where(e => subjectIds.Contains(e.UserSubjectId))
            .ToListAsync(ct));
        repository.RemoveRange(deadlines);
        repository.RemoveRange(await repository.Query<StudySession>(false)
            .Where(s => s.UserId == userId && s.UserSubjectId != null && subjectIds.Contains(s.UserSubjectId.Value))
            .ToListAsync(ct));

        // Q25: giữ nội dung Note / Document / FlashcardDeck (và mọi bảng con, file vật lý), chỉ gỡ liên kết môn.
        // Cập nhật trực tiếp một cột UserSubjectId (không tải cả entity) nên không phụ thuộc các cột khác của
        // bảng thuộc module khác. Lệnh chạy ngay trong transaction hiện tại; lỗi sau đó vẫn rollback được.
        await repository.Query<Note>(false)
            .Where(n => n.UserSubjectId != null && subjectIds.Contains(n.UserSubjectId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.UserSubjectId, (long?)null), ct);
        await repository.Query<Document>(false)
            .Where(d => d.UserSubjectId != null && subjectIds.Contains(d.UserSubjectId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UserSubjectId, (long?)null), ct);
        await repository.Query<FlashcardDeck>(false)
            .Where(d => d.UserSubjectId != null && subjectIds.Contains(d.UserSubjectId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UserSubjectId, (long?)null), ct);

        repository.RemoveRange(await repository.Query<UserSubject>(false)
            .Where(us => us.UserId == userId && subjectIds.Contains(us.UserSubjectId))
            .ToListAsync(ct));

        return new DeletionScope(subjectIds, goalIds, eventIds, deadlineIds);
    }

    /// <summary>Hậu kiểm trong transaction: còn bản ghi tham chiếu phạm vi đã xóa → ném lỗi để rollback.</summary>
    private async Task EnsureNoOrphansAsync(DeletionScope scope, List<long> columnIds, long? gradeBookId, CancellationToken ct)
    {
        var s = scope.SubjectIds;
        var g = scope.GoalIds;
        var e = scope.ScheduleEventIds;
        var d = scope.DeadlineIds;

        var remaining =
            await repository.Query<UserSubject>().CountAsync(x => s.Contains(x.UserSubjectId), ct) +
            await repository.Query<GradeEntry>().CountAsync(x => s.Contains(x.UserSubjectId) || columnIds.Contains(x.GradeColumnId), ct) +
            await repository.Query<Goal>().CountAsync(x => s.Contains(x.UserSubjectId), ct) +
            await repository.Query<GoalHistory>().CountAsync(x => g.Contains(x.GoalId), ct) +
            await repository.Query<GoalProgressLog>().CountAsync(x =>
                s.Contains(x.UserSubjectId) || (x.GoalId != null && g.Contains(x.GoalId.Value)), ct) +
            await repository.Query<ScheduleEvent>().CountAsync(x =>
                (x.GoalId != null && g.Contains(x.GoalId.Value)) || (x.UserSubjectId != null && s.Contains(x.UserSubjectId.Value)), ct) +
            await repository.Query<ScheduleException>().CountAsync(x => e.Contains(x.ScheduleEventId), ct) +
            await repository.Query<Reminder>().CountAsync(x =>
                (x.ScheduleEventId != null && e.Contains(x.ScheduleEventId.Value)) || (x.DeadlineId != null && d.Contains(x.DeadlineId.Value)), ct) +
            await repository.Query<Deadline>().CountAsync(x => x.UserSubjectId != null && s.Contains(x.UserSubjectId.Value), ct) +
            await repository.Query<StudySession>().CountAsync(x => x.UserSubjectId != null && s.Contains(x.UserSubjectId.Value), ct) +
            await repository.Query<Note>().CountAsync(x => x.UserSubjectId != null && s.Contains(x.UserSubjectId.Value), ct) +
            await repository.Query<Document>().CountAsync(x => x.UserSubjectId != null && s.Contains(x.UserSubjectId.Value), ct) +
            await repository.Query<FlashcardDeck>().CountAsync(x => x.UserSubjectId != null && s.Contains(x.UserSubjectId.Value), ct);

        if (gradeBookId.HasValue)
        {
            remaining +=
                await repository.Query<GradeColumn>().CountAsync(x => x.GradeBookId == gradeBookId.Value, ct) +
                await repository.Query<GradeBook>().CountAsync(x => x.GradeBookId == gradeBookId.Value, ct);
        }

        if (remaining > 0)
            throw new InvalidOperationException("Không thể xóa trọn vẹn dữ liệu, thao tác đã được hủy và dữ liệu chưa bị thay đổi. Vui lòng thử lại.");
    }
}
