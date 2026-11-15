namespace StudyHub.Web.ViewModels.NotesV3;

// Màn 1: thư viện notebook và PDF
public sealed record NotesIndexViewModel(
    int NotebookCount,
    int PdfCount,
    int SharedCount,
    IReadOnlyList<NotebookCardViewModel> Notebooks,
    IReadOnlyList<PdfCardViewModel> Pdfs,
    IReadOnlyList<GroupOptionViewModel> Groups,
    GroupOptionViewModel? SelectedGroup,
    IReadOnlyList<NotebookCardViewModel> GroupNotebooks,
    IReadOnlyList<SharedPdfCardViewModel> GroupPdfs);

public sealed record NotesTrashViewModel(
    IReadOnlyList<TrashedNotebookViewModel> Notebooks,
    IReadOnlyList<TrashedPdfViewModel> Pdfs);

public sealed record TrashedNotebookViewModel(long Id, string Title, int DaysLeft);

public sealed record TrashedPdfViewModel(
    long Id, string Title, string FileName, string VersionToken, int DaysLeft);

public sealed record NotebookCardViewModel(
    long Id,
    string Title);

public sealed record PdfCardViewModel(
    long Id,
    string Title,
    string FileName,
    int PageCount,
    bool IsArchived,
    string VersionToken);

public sealed record GroupOptionViewModel(
    long Id,
    string Name);

public sealed record SharedPdfCardViewModel(
    long Id,
    long ShareId,
    string Title,
    string FileName,
    int PageCount);
