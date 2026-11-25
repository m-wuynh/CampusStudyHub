using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Ai;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.Web.Filters.Pdf;
using StudyHub.Web.Services.Pdf;

namespace StudyHub.Web.Configuration;

public static class PdfServiceCollectionExtensions
{
    public static IServiceCollection AddPdfModule(
        this IServiceCollection services)
    {
        services.AddScoped<PdfLibraryService>();
        services.AddScoped<PdfDocumentService>();
        services.AddScoped<PdfAiService>();

        services.AddScoped<IPdfFileStorage, LocalPdfFileStorage>();
        services.AddHttpClient<IPdfAiClient, GeminiPdfAiClient>();

        services.AddScoped<PdfExceptionFilter>();
        services.AddHostedService<
            StudyHub.Web.BackgroundServices.PdfTrashBackgroundService>();

        return services;
    }
}
