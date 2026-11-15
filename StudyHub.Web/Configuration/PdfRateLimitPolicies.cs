using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace StudyHub.Web.Configuration;

public static class PdfRateLimitPolicies
{
    public const string Uploads = "PdfUploads";
    public const string Writes = "PdfWrites";

    public static IServiceCollection AddPdfRateLimits(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Uploads, context => FixedWindow(
                context,
                permitLimit: 5,
                window: TimeSpan.FromMinutes(10)));
            options.AddPolicy(Writes, context => FixedWindow(
                context,
                permitLimit: 120,
                window: TimeSpan.FromMinutes(1)));
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(
        HttpContext context,
        int permitLimit,
        TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = window
            });

    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}
