using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using StudyHub.Web.Services.Pdf;

namespace StudyHub.Pdf.Tests;

public sealed class GeminiPdfAiClientTests
{
    [Fact]
    public async Task Analyze_RetriesOneTemporaryFailure()
    {
        var handler = new SequenceHandler(
            new(HttpStatusCode.ServiceUnavailable),
            JsonResponse());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key",
                ["Gemini:Model"] = "test-model"
            })
            .Build();
        var client = new GeminiPdfAiClient(
            new HttpClient(handler),
            configuration);

        var response = await client.AnalyzeAsync("source text");

        Assert.Equal(2, handler.CallCount);
        Assert.Equal("Short summary", response.Result.Summary);
        Assert.Equal(12, response.InputTokens);
        Assert.Equal(5, response.OutputTokens);
    }

    [Fact]
    public async Task Analyze_UsesBackupModelWhenLiteModelStaysBusy()
    {
        var handler = new SequenceHandler(
            new(HttpStatusCode.ServiceUnavailable),
            new(HttpStatusCode.ServiceUnavailable),
            JsonResponse());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key",
                ["Gemini:Model"] = "gemini-flash-lite-latest"
            })
            .Build();
        var client = new GeminiPdfAiClient(
            new HttpClient(handler),
            configuration);

        await client.AnalyzeAsync("source text");

        Assert.Equal(3, handler.CallCount);
        Assert.EndsWith(
            "/gemini-3.6-flash:generateContent",
            handler.RequestUris[^1]);
    }

    private static HttpResponseMessage JsonResponse()
    {
        const string response = """
            {
              "candidates": [{
                "content": { "parts": [{
                  "text": "{\"summary\":\"Short summary\",\"keyPoints\":[],\"concepts\":[]}"
                }]}
              }],
              "usageMetadata": {
                "promptTokenCount": 12,
                "candidatesTokenCount": 5
              }
            }
            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }

    private sealed class SequenceHandler(
        params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public List<string> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(responses[CallCount++]);
        }
    }
}
