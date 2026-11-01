namespace StudyHub.Web.ViewModels.NotesV3;
// Màn 2: một notebook, các section và trang
public sealed record NotesNotebookViewModel(
    long Id,
    string Title,
    IReadOnlyList<SectionViewModel> Sections,
    long? SelectedSectionId,
    PageEditorViewModel? CurrentPage);

public sealed record SectionViewModel(
    long Id,
    string Title,
    IReadOnlyList<PageLinkViewModel> Pages);

public sealed record PageLinkViewModel(
    long Id,
    string Title);

public sealed record PageEditorViewModel(
    long Id,
    string Title,
    string Text,
    string VersionToken);
