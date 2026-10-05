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
        if (alreadyExists) throw new InvalidOperationException($"Bạn đã có sổ điểm năm học {dto.FromYear}–{dto.ToYear}. Không thể tạo trùng.");

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
        var gradeBook = await repository.Query<GradeBook>(false)
            .FirstOrDefaultAsync(gb => gb.GradeBookId == gradeBookId, cancellationToken);

        if (gradeBook is null) return;
        if (gradeBook.UserId != userId) throw new UnauthorizedAccessException("Không có quyền truy cập.");

        // 1. Get all GradeColumns for this GradeBook
        var columns = await repository.Query<GradeColumn>(false)
            .Where(c => c.GradeBookId == gradeBookId)
            .ToListAsync(cancellationToken);

        var columnIds = columns.Select(c => c.GradeColumnId).ToList();

        // 2. Load & Remove GradeEntries
        if (columnIds.Any())
        {
            var entries = await repository.Query<GradeEntry>(false)
                .Where(ge => columnIds.Contains(ge.GradeColumnId))
                .ToListAsync(cancellationToken);
            foreach(var e in entries) repository.Remove(e);
        }

        // 3. Remove GradeColumns
        foreach(var c in columns) repository.Remove(c);

        // 4. Remove GradeBook
        repository.Remove(gradeBook);

        // 5. Commit
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<GradeBookDto> CreateGradeBookWithColumnsAsync(long userId, CreateGradeBookWithColumnsDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.FromYear >= dto.ToYear) throw new ArgumentException("Năm kết thúc phải lớn hơn năm bắt đầu.");

        var alreadyExists = await repository.AnyAsync<GradeBook>(
            gb => gb.UserId == userId && gb.FromYear == dto.FromYear && gb.ToYear == dto.ToYear, cancellationToken);
        if (alreadyExists) throw new InvalidOperationException($"Bạn đã có sổ điểm năm học {dto.FromYear}–{dto.ToYear}. Không thể tạo trùng.");

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

        // Add Subjects
        if (dto.SubjectNames != null && dto.SubjectNames.Any())
        {
            foreach (var subj in dto.SubjectNames)
            {
                if (string.IsNullOrWhiteSpace(subj)) continue;
                await repository.AddAsync(new UserSubject
                {
                    UserId = userId,
                    GradeBookId = gradeBook.GradeBookId,
                    CustomSubjectName = subj.Trim(),
                    CreditWeight = 1,
                    ColorHex = "#4f46e5"
                }, cancellationToken);
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
