namespace StudyHub.BLL.Services.Dashboard;

/// <summary>Một trang ghi chú NotesV3 hiển thị trên Dashboard.</summary>
public sealed record DashboardNoteDto(
    long PageId,
    long SectionId,
    long NotebookId,
    string Title,
    string Summary,
    string NotebookTitle);

/// <summary>
/// Kết quả đọc ghi chú gần đây. <see cref="IsAvailable"/> = false khi CSDL chưa có bảng NotesV3
/// (hoặc truy vấn lỗi) – Dashboard vẫn hiển thị bình thường, chỉ báo widget chưa khả dụng.
/// </summary>
public sealed record DashboardNotesResult(bool IsAvailable, IReadOnlyList<DashboardNoteDto> Notes);

public interface IDashboardNotesService
{
    Task<DashboardNotesResult> GetRecentNotesAsync(long userId, int take, CancellationToken ct = default);
}
