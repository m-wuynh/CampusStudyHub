using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.DTOs.Pdf;
using StudyHub.BLL.Services.Auth;
using StudyHub.BLL.Services.Pdf.Documents;
using StudyHub.DAL.Entities.Pdf;
using StudyHub.DAL.Repositories.Common;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace StudyHub.BLL.Services.Pdf.Ai;

public sealed partial class PdfAiService(
    IRepository repository,
    ICurrentUser currentUser,
    PdfDocumentService documentService,
    IPdfFileStorage fileStorage,
    IPdfAiClient aiClient)
{
    private const int MinTextLength = 50;
    private const string PromptVersion = "v1";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<long> AnalyzeAsync(
        long documentId,
        int fromPage,
        int toPage,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var document = await documentService.RequireDocumentAsync(
            documentId, cancellationToken);

        ValidatePages(fromPage, toPage, document.PageCount);

        await using var file = await fileStorage.OpenReadAsync(
            document.StorageKey, cancellationToken);
        var text = ExtractText(file, fromPage, toPage, cancellationToken);

        if (text.Length < MinTextLength)
        {
            throw new PdfException(400,
                "Các trang đã chọn không có đủ nội dung.");
        }

        var hash = Hash(text);
        var cachedId = await FindCacheAsync(
            userId, documentId, fromPage, toPage, hash, cancellationToken);
        if (cachedId.HasValue)
        {
            return cachedId.Value;
        }

        await CheckQuotaAsync(userId, cancellationToken);
        var response = await aiClient.AnalyzeAsync(text, cancellationToken);
        ValidateResult(response.Result);

        var trackedDocument = await documentService.RequireDocumentAsync(
            documentId, cancellationToken, trackChanges: true);
        var now = DateTime.UtcNow;
        trackedDocument.UpdatedAtUtc = now;

        var analysis = new PdfAiAnalysis
        {
            OwnerUserId = userId,
            DocumentId = documentId,
            FromPage = fromPage,
            ToPage = toPage,
            SourceHash = hash,
            Model = aiClient.Model,
            PromptVersion = PromptVersion,
            Status = PdfAiAnalysis.Draft,
            ResultJson = Serialize(response.Result),
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(analysis, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return analysis.AnalysisId;
    }

    public async Task<PdfAnalysisDto> GetAsync(
        long analysisId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var row = await (
            from analysis in OwnedAnalyses(userId)
            join document in repository.Query<PdfDocument>()
                on analysis.DocumentId equals document.DocumentId
            where analysis.AnalysisId == analysisId && !document.IsDeleted
            select new { analysis, document.Title })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound();

        return new PdfAnalysisDto(
            row.analysis.AnalysisId,
            row.analysis.DocumentId,
            row.Title,
            row.analysis.FromPage,
            row.analysis.ToPage,
            row.analysis.Status,
            row.analysis.Status == PdfAiAnalysis.Draft,
            row.analysis.Status is PdfAiAnalysis.Draft or PdfAiAnalysis.Accepted,
            Deserialize(row.analysis.ResultJson),
            row.analysis.InputTokens,
            row.analysis.OutputTokens);
    }

    public async Task AcceptAsync(
        long analysisId,
        PdfAiResult result,
        CancellationToken cancellationToken = default)
    {
        ValidateResult(result);
        var userId = currentUser.RequirePdfUserId();
        var analysis = await OwnedAnalyses(userId, true)
            .SingleOrDefaultAsync(
                item => item.AnalysisId == analysisId,
                cancellationToken)
            ?? throw NotFound();

        if (analysis.Status is not PdfAiAnalysis.Draft and
            not PdfAiAnalysis.Accepted)
        {
            throw new PdfException(409, "Kết quả AI đã bị bỏ.");
        }

        await documentService.RequireDocumentAsync(
            analysis.DocumentId, cancellationToken);

        var now = DateTime.UtcNow;
        var oldResult = Deserialize(analysis.ResultJson);
        await SavePageNoteAsync(
            analysis, result, oldResult, userId, now, cancellationToken);

        analysis.Status = PdfAiAnalysis.Accepted;
        analysis.ResultJson = Serialize(result);
        analysis.UpdatedAtUtc = now;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DiscardAsync(
        long analysisId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequirePdfUserId();
        var analysis = await OwnedAnalyses(userId, true)
            .SingleOrDefaultAsync(
                item => item.AnalysisId == analysisId,
                cancellationToken)
            ?? throw NotFound();

        if (analysis.Status == PdfAiAnalysis.Accepted)
        {
            throw new PdfException(409,
                "Kết quả đã duyệt không thể bỏ.");
        }

        analysis.Status = PdfAiAnalysis.Discarded;
        analysis.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task SavePageNoteAsync(
        PdfAiAnalysis analysis,
        PdfAiResult result,
        PdfAiResult oldResult,
        long userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var note = await repository.Query<PdfDocumentPageNote>(false)
            .SingleOrDefaultAsync(item =>
                item.OwnerUserId == userId &&
                item.DocumentId == analysis.DocumentId &&
                item.PhysicalPageNumber == analysis.FromPage,
                cancellationToken);

        var pages = analysis.FromPage == analysis.ToPage
            ? $"{analysis.FromPage}"
            : $"{analysis.FromPage}-{analysis.ToPage}";
        var oldText = note?.ContentText ?? string.Empty;
        var oldBlock = FormatNote(pages, oldResult);
        var content = oldText.Replace(oldBlock, string.Empty).Trim();
        var newBlock = FormatNote(pages, result);
        content = string.IsNullOrEmpty(content)
            ? newBlock
            : $"{content}\n\n{newBlock}";

        if (content.Length > PdfPolicy.MaxNoteLength)
        {
            throw new PdfException(400, "Ghi chú trang nguồn quá dài.");
        }

        if (note is null)
        {
            await repository.AddAsync(new PdfDocumentPageNote
            {
                OwnerUserId = userId,
                DocumentId = analysis.DocumentId,
                PhysicalPageNumber = analysis.FromPage,
                ContentText = content,
                UpdatedAtUtc = now
            }, cancellationToken);
            return;
        }

        note.ContentText = content;
        note.UpdatedAtUtc = now;
    }

    private static string FormatNote(string pages, PdfAiResult result)
    {
        var note = new StringBuilder()
            .Append("[AI tổng hợp trang ").Append(pages).AppendLine("]")
            .AppendLine("Tóm tắt:").AppendLine(result.Summary.Trim());

        if (result.KeyPoints.Count > 0)
        {
            note.AppendLine().AppendLine("Ý chính:");
            foreach (var point in result.KeyPoints)
            {
                note.Append("- ").AppendLine(point.Trim());
            }
        }

        if (result.Concepts.Count > 0)
        {
            note.AppendLine().AppendLine("Khái niệm và câu hỏi:");
            for (var i = 0; i < result.Concepts.Count; i++)
            {
                var concept = result.Concepts[i];
                note.Append(i + 1).Append(". ")
                    .AppendLine(concept.Title.Trim())
                    .AppendLine(concept.Summary.Trim())
                    .Append("Câu hỏi: ").AppendLine(concept.Question.Trim())
                    .Append("Đáp án: ").AppendLine(concept.Answer.Trim());
            }
        }

        return note.ToString().TrimEnd();
    }

    private async Task<long?> FindCacheAsync(
        long userId,
        long documentId,
        int fromPage,
        int toPage,
        string hash,
        CancellationToken cancellationToken) =>
        await OwnedAnalyses(userId)
            .Where(item =>
                item.DocumentId == documentId &&
                item.FromPage == fromPage &&
                item.ToPage == toPage &&
                item.SourceHash == hash &&
                item.Model == aiClient.Model &&
                item.PromptVersion == PromptVersion &&
                item.Status != PdfAiAnalysis.Discarded)
            .OrderByDescending(item => item.AnalysisId)
            .Select(item => (long?)item.AnalysisId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task CheckQuotaAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var count = await repository.Query<PdfAiAnalysis>()
            .CountAsync(
                PdfPolicy.AiRequestsUsedToday(userId, DateTime.UtcNow),
                cancellationToken);

        if (count >= PdfPolicy.MaxAiRequestsPerDay)
        {
            throw new PdfException(429,
                $"Bạn đã dùng hết {PdfPolicy.MaxAiRequestsPerDay} lượt AI hôm nay.");
        }
    }

    private static string ExtractText(
        Stream stream,
        int fromPage,
        int toPage,
        CancellationToken cancellationToken)
    {
        using var document = PdfPigDocument.Open(stream);
        var text = new StringBuilder();

        for (var page = fromPage;
             page <= toPage && text.Length < PdfPolicy.MaxAiExtractedCharacters;
             page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageText = Spaces().Replace(
                document.GetPage(page).Text, " ");
            text.Append("[Trang ").Append(page).AppendLine("]")
                .AppendLine(pageText);
        }

        return text.ToString()[..Math.Min(
            text.Length,
            PdfPolicy.MaxAiExtractedCharacters)];
    }

    private static void ValidatePages(int fromPage, int toPage, int pageCount)
    {
        var count = toPage - fromPage + 1;
        if (fromPage < 1 || toPage < fromPage || toPage > pageCount ||
            count > PdfPolicy.MaxAiPagesPerRequest)
        {
            throw new PdfException(400,
                $"Chỉ được chọn tối đa {PdfPolicy.MaxAiPagesPerRequest} trang.");
        }
    }

    private static void ValidateResult(PdfAiResult result)
    {
        var valid = !string.IsNullOrWhiteSpace(result.Summary) &&
                    result.Summary.Length <= PdfPolicy.MaxAiSummaryLength &&
                    result.KeyPoints.Count <= PdfPolicy.MaxAiKeyPointCount &&
                    result.Concepts.Count is >= 1 and <= PdfPolicy.MaxAiConceptCount &&
                    result.KeyPoints.All(point =>
                        !string.IsNullOrWhiteSpace(point) &&
                        point.Length <= PdfPolicy.MaxAiKeyPointLength) &&
                    result.Concepts.All(concept =>
                        !string.IsNullOrWhiteSpace(concept.Title) &&
                        concept.Title.Length <= PdfPolicy.MaxAiConceptTitleLength &&
                        concept.Summary.Length <= PdfPolicy.MaxAiConceptSummaryLength &&
                        concept.Question.Length <= PdfPolicy.MaxAiQuestionLength &&
                        concept.Answer.Length <= PdfPolicy.MaxAiAnswerLength);

        if (!valid)
        {
            throw new PdfException(400, "Kết quả AI không hợp lệ.");
        }
    }

    private IQueryable<PdfAiAnalysis> OwnedAnalyses(
        long userId,
        bool trackChanges = false) =>
        repository.Query<PdfAiAnalysis>(!trackChanges)
            .Where(item => item.OwnerUserId == userId);

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Serialize(PdfAiResult result) =>
        JsonSerializer.Serialize(result, JsonOptions);

    private static PdfAiResult Deserialize(string json) =>
        JsonSerializer.Deserialize<PdfAiResult>(json, JsonOptions)
        ?? throw new PdfException(500, "Dữ liệu AI đã lưu không hợp lệ.");

    private static PdfException NotFound() =>
        new(404, "Không tìm thấy kết quả AI.");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
