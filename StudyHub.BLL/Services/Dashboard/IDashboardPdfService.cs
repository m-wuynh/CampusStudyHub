namespace StudyHub.BLL.Services.Dashboard;

public sealed record DashboardPdfDto(
    long DocumentId,
    string Title,
    string FileName,
    DateTime UpdatedAtUtc);

public interface IDashboardPdfService
{
    Task<IReadOnlyList<DashboardPdfDto>> GetRecentAsync(
        long userId,
        int take,
        CancellationToken cancellationToken = default);
}
