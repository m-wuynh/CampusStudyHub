using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudyHub.BLL.Services.NotesV3;
using StudyHub.DAL.Entities.NotesV3;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Dashboard;

/// <summary>
/// Đọc (chỉ đọc) các trang ghi chú cá nhân mới nhất trong sổ tay NotesV3 của người dùng.
/// NotesV3Page không có cột thời gian cập nhật nên "gần đây" = PageId lớn nhất (trang tạo sau cùng).
/// </summary>
public sealed class DashboardNotesService(
    IRepository repository,
    ILogger<DashboardNotesService> logger) : IDashboardNotesService
{
    public const int SummaryMaxLength = 120;

    public async Task<DashboardNotesResult> GetRecentNotesAsync(long userId, int take, CancellationToken ct = default)
    {
        if (userId <= 0 || take <= 0)
        {
            return new DashboardNotesResult(true, []);
        }

        try
        {
            var rows = await (
                from page in repository.Query<NotesV3Page>()
                join section in repository.Query<NotesV3Section>() on page.SectionId equals section.SectionId
                join notebook in repository.Query<NotesV3Notebook>() on section.NotebookId equals notebook.NotebookId
                where notebook.OwnerUserId == userId
                      && notebook.StudyGroupId == null
                      && !notebook.IsDeleted
                      && !section.IsDeleted
                      && !page.IsDeleted
                orderby page.PageId descending
                select new
                {
                    page.PageId,
                    section.SectionId,
                    notebook.NotebookId,
                    page.Title,
                    page.ContentDeltaJson,
                    NotebookTitle = notebook.Title
                })
                .Take(take)
                .ToListAsync(ct);

            var notes = rows
                .Select(x => new DashboardNoteDto(
                    x.PageId,
                    x.SectionId,
                    x.NotebookId,
                    string.IsNullOrWhiteSpace(x.Title) ? "Trang không tiêu đề" : x.Title.Trim(),
                    Summarize(x.ContentDeltaJson),
                    x.NotebookTitle))
                .ToList();

            return new DashboardNotesResult(true, notes);
        }
        catch (DbException ex)
        {
            // Ví dụ: CSDL chưa có các bảng NotesV3 (migration NotesV3IndependentDal chưa áp dụng).
            logger.LogWarning(ex, "Không đọc được ghi chú NotesV3 cho Dashboard (user {UserId}).", userId);
            return new DashboardNotesResult(false, []);
        }
    }

    private static string Summarize(string? deltaJson)
    {
        if (string.IsNullOrWhiteSpace(deltaJson))
        {
            return string.Empty;
        }

        string text;
        try
        {
            text = TextDelta.ToText(deltaJson);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return string.Empty;
        }

        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= SummaryMaxLength
            ? collapsed
            : collapsed[..SummaryMaxLength].TrimEnd() + "…";
    }
}
