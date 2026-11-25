namespace StudyHub.BLL.DTOs.Pdf;

public sealed record PdfLibraryDto(
    int DocumentCount,
    IReadOnlyList<PdfSubjectFolderDto> Folders,
    IReadOnlyList<PdfDocumentItemDto> RecentDocuments);

public sealed record PdfSubjectFolderDto(
    long? UserSubjectId,
    string Name,
    string ColorHex,
    int DocumentCount);

public sealed record PdfFolderDetailsDto(
    long? UserSubjectId,
    string Name,
    string ColorHex,
    IReadOnlyList<PdfDocumentItemDto> Documents);

public sealed record PdfDocumentItemDto(
    long DocumentId,
    string? SubjectName,
    string Title,
    string OriginalFileName,
    int PageCount);

public sealed record PdfTrashItemDto(
    long DocumentId,
    string Title,
    string OriginalFileName,
    DateTime DeletedAtUtc,
    DateTime DeletePermanentlyAtUtc);

public sealed record PdfReaderDto(
    long DocumentId,
    long? UserSubjectId,
    string Title,
    string FileName,
    int PageCount,
    int CurrentPage,
    string NoteText,
    IReadOnlyList<int> NotePages,
    int AiRequestsUsedToday,
    int AiDailyLimit);

public sealed record PdfAiResult(
    string Summary,
    IReadOnlyList<string> KeyPoints,
    IReadOnlyList<PdfAiConcept> Concepts);

public sealed record PdfAiConcept(
    string Title,
    string Summary,
    string Question,
    string Answer);

public sealed record PdfAnalysisDto(
    long AnalysisId,
    long DocumentId,
    string DocumentTitle,
    int FromPage,
    int ToPage,
    string Status,
    bool IsDraft,
    bool CanAccept,
    PdfAiResult Result,
    int InputTokens,
    int OutputTokens);

public sealed record PdfDashboardDto(
    long? UserSubjectId,
    string SubjectName,
    int DocumentCount,
    int NotePageCount,
    int AcceptedAnalysisCount,
    int AiRequestsUsedToday,
    int AiDailyLimit,
    int TotalInputTokens,
    int TotalOutputTokens);
