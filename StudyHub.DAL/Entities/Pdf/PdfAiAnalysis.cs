namespace StudyHub.DAL.Entities.Pdf;

public sealed class PdfAiAnalysis
{
    public const string Draft = "Draft";
    public const string Accepted = "Accepted";
    public const string Discarded = "Discarded";

    public long AnalysisId { get; set; }

    public long OwnerUserId { get; set; }

    public long DocumentId { get; set; }

    public int FromPage { get; set; }

    public int ToPage { get; set; }

    public string SourceHash { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;

    public string Status { get; set; } = Draft;

    public string ResultJson { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
