namespace StudyHub.BLL.DTOs.NotesV3;

public sealed record NotebookItem(
    long Id,
    string Title,
    int SortOrder,
    long? GroupId);

public sealed record SectionItem(
    long Id,
    string Title,
    int SortOrder);

public sealed record PageItem(
    long Id,
    long SectionId,
    string Title,
    int SortOrder,
    string VersionToken);

public sealed record NotebookDetails(
    NotebookItem Notebook,
    IReadOnlyList<SectionItem> Sections,
    IReadOnlyList<PageItem> Pages);

public sealed record PageDetails(
    long Id,
    string Title,
    string? DeltaJson,
    string VersionToken,
    bool IsGroup);

public sealed record PageSaveRequest(
    string DeltaJson,
    string VersionToken);

public sealed record PageSaveResult(string VersionToken);
public sealed record NotebookPageSeed(
    long SourcePageId,
    string SourceVersionToken,
    byte[] InitialUpdate);

public sealed record CopyNotebookRequest(
    long SourceNotebookId,
    long GroupId,
    IReadOnlyList<NotebookPageSeed> Seeds);

public sealed record CopyNotebookResult(
    long NotebookId,
    long GroupId);
public sealed record NotesDashboardDto(
    int PersonalPdfCount,
    int PersonalNotebookCount,
    int SharedPdfCount,
    int GroupNotebookCount)
{
    public int SharedResourceCount =>
        SharedPdfCount + GroupNotebookCount;
}