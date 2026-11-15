using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Access;
using StudyHub.BLL.Services.Pdf.Annotations;
using StudyHub.BLL.Services.Pdf.Notes;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.BLL.Services.Pdf.Sharing;
using StudyHub.BLL.Services.Pdf.Trash;
using StudyHub.DAL.Repositories.Pdf;
using StudyHub.Web.BackgroundServices;
using StudyHub.Web.Filters.Pdf;
using StudyHub.Web.Services.Pdf;

namespace StudyHub.Web.Configuration;

public static class PdfServiceCollectionExtensions
{
    public static IServiceCollection AddPdfModule(
        this IServiceCollection services)
    {
        services.AddPdfRateLimits();
        services.AddScoped<PdfAccessService>();
        services.AddScoped<PdfShareStore>();
        // PdfSharingService (BLL) implement IPdfSharingService (DAL contract)
        // để SqlGroupLearningRepository inject mà không tạo DAL → BLL dependency.
        services.AddScoped<IPdfSharingService, PdfSharingService>();
        services.AddScoped<PdfLibraryService>();
        services.AddScoped<PdfService>();
        services.AddScoped<PdfPageNotesService>();
        services.AddScoped<PdfMarksService>();
        services.AddScoped<PdfTrashPurgeService>();
        services.AddScoped<IPdfFileStorage, LocalPdfFileStorage>();
        services.AddScoped<PdfExceptionFilter>();
        services.AddHostedService<PdfTrashBackgroundService>();

        return services;
    }
}
