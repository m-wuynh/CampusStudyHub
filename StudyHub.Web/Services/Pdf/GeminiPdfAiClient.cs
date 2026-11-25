using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Pdf;
using StudyHub.BLL.Services.Pdf.Ai;

namespace StudyHub.Web.Services.Pdf;

public sealed class GeminiPdfAiClient(
    HttpClient httpClient,
    IConfiguration configuration) : IPdfAiClient
{
    private const string DefaultModel = "gemini-flash-lite-latest";
    private const string BackupModel = "gemini-3.6-flash";
    private const string Endpoint =
        "https://generativelanguage.googleapis.com/v1beta/models";

    private const string Prompt = """
        Phân tích nội dung học tập dưới đây bằng tiếng Việt.
        Chỉ dùng thông tin có trong nội dung.
        Trả về JSON gồm summary, keyPoints và concepts.
        Mỗi concept phải có title, summary, question và answer.
        """;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public string Model =>
        configuration["Gemini:Model"] ?? DefaultModel;

    public async Task<PdfAiResponse> AnalyzeAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new PdfException(503, "Chưa cấu hình Gemini:ApiKey.");
        }

        using var response = await SendAsync(
            Model,
            apiKey,
            text,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw ToPdfException(response.StatusCode);
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        var root = json.RootElement;
        var resultJson = root
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        var result = JsonSerializer.Deserialize<PdfAiResult>(
                resultJson ?? "",
                JsonOptions)
            ?? throw InvalidResponse();

        var usage = root.TryGetProperty("usageMetadata", out var value)
            ? value
            : default;

        return new PdfAiResponse(
            result,
            ReadToken(usage, "promptTokenCount"),
            ReadToken(usage, "candidatesTokenCount"));
    }

    private async Task<HttpResponseMessage> SendAsync(
        string model,
        string apiKey,
        string text,
        CancellationToken cancellationToken)
    {
        var response = await SendOnceAsync(
            model, apiKey, text, cancellationToken);

        if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
        {
            return response;
        }

        response.Dispose();
        response = await SendOnceAsync(
            model, apiKey, text, cancellationToken);

        if (response.StatusCode != HttpStatusCode.ServiceUnavailable ||
            model == BackupModel)
        {
            return response;
        }

        response.Dispose();
        return await SendOnceAsync(
            BackupModel, apiKey, text, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        string model,
        string apiKey,
        string text,
        CancellationToken cancellationToken)
    {
        var url = $"{Endpoint}/{Uri.EscapeDataString(model)}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(CreateBody(text));
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static object CreateBody(string text) => new
    {
        contents = new[]
        {
            new
            {
                parts = new[]
                {
                    new { text = Prompt + "\n" + text }
                }
            }
        },
        generationConfig = new
        {
            temperature = 0.2,
            responseMimeType = "application/json"
        }
    };

    private static int ReadToken(
        JsonElement usage,
        string name) =>
        usage.ValueKind == JsonValueKind.Object &&
        usage.TryGetProperty(name, out var value)
            ? value.GetInt32()
            : 0;

    private static PdfException InvalidResponse() =>
        new(502, "Gemini trả về dữ liệu không hợp lệ.");

    private PdfException ToPdfException(
        System.Net.HttpStatusCode statusCode) =>
        statusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests =>
                new(429, "Gemini đang giới hạn request."),
            System.Net.HttpStatusCode.ServiceUnavailable =>
                new(503, "Gemini đang quá tải. Hãy thử lại sau."),
            System.Net.HttpStatusCode.Unauthorized or
            System.Net.HttpStatusCode.Forbidden =>
                new(502, "Gemini từ chối API key."),
            _ => new(502, "Gemini không thể xử lý yêu cầu.")
        };
}
