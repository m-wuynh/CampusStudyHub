namespace StudyHub.BLL.DTOs.NotesV3;

public sealed record NotesIndexDto(
    int NotebookCount,
    int PdfCount,
    int SharedCount,
    IReadOnlyList<NotebookSummaryDto> Notebooks,
    IReadOnlyList<PdfSummaryDto> Pdfs,
    IReadOnlyList<GroupSummaryDto> Groups,
    GroupSummaryDto? SelectedGroup,
    IReadOnlyList<NotebookSummaryDto> GroupNotebooks,
    IReadOnlyList<SharedPdfSummaryDto> GroupPdfs);

public sealed record NotesTrashDto(
    IReadOnlyList<TrashedNotebookDto> Notebooks,
    IReadOnlyList<TrashedPdfDto> Pdfs);

public sealed record TrashedNotebookDto(long Id, string Title, int DaysLeft);

public sealed record TrashedPdfDto(
    long Id, string Title, string FileName, string VersionToken, int DaysLeft);

public sealed record NotebookSummaryDto(
    long Id,
    string Title);

public sealed record PdfSummaryDto(
    long Id,
    string Title,
    string FileName,
    int PageCount,
    bool IsArchived,
    string VersionToken);

public sealed record GroupSummaryDto(
    long Id,
    string Name);

public sealed record SharedPdfSummaryDto(
    long Id,
    long ShareId,
    string Title,
    string FileName,
    int PageCount);
