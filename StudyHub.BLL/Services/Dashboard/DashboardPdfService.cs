using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Dashboard;

public sealed class DashboardPdfService(
    IRepository repository) : IDashboardPdfService
{
    public async Task<IReadOnlyList<DashboardPdfDto>> GetRecentAsync(
        long userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || take <= 0)
        {
            return [];
        }

        return await repository.Query<PdfDocument>()
            .Where(document =>
                document.OwnerUserId == userId &&
                !document.IsDeleted)
            .OrderByDescending(document => document.UpdatedAtUtc)
            .Take(Math.Min(take, 12))
            .Select(document => new DashboardPdfDto(
                document.DocumentId,
                document.Title,
                document.OriginalFileName,
                document.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
