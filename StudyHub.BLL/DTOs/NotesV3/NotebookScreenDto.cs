namespace StudyHub.BLL.DTOs.NotesV3;

public sealed record NotebookScreenDto(
    long Id,
    string Title,
    IReadOnlyList<NotebookSectionDto> Sections,
    long? SelectedSectionId,
    NotebookPageDto? CurrentPage);

public sealed record NotebookSectionDto(
    long Id,
    string Title,
    IReadOnlyList<NotebookPageLinkDto> Pages);

public sealed record NotebookPageLinkDto(
    long Id,
    string Title);

public sealed record NotebookPageDto(
    long Id,
    string Title,
    string Text,
    string VersionToken);
