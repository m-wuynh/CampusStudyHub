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
    GradeBookDeletionService deletionService,
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

    public Task DeleteGradeBookAsync(long userId, long gradeBookId, CancellationToken cancellationToken = default) =>
        deletionService.DeleteGradeBookAsync(userId, gradeBookId, cancellationToken);

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
}
