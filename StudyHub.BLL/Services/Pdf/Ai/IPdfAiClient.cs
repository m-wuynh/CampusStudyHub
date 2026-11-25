using StudyHub.BLL.DTOs.Pdf;

namespace StudyHub.BLL.Services.Pdf.Ai;

public sealed record PdfAiResponse(
    PdfAiResult Result,
    int InputTokens,
    int OutputTokens);

public interface IPdfAiClient
{
    string Model { get; }

    Task<PdfAiResponse> AnalyzeAsync(
        string text,
        CancellationToken cancellationToken = default);
}
