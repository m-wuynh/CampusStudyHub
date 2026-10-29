using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.Web.ViewModels.NotesV3;

public sealed record NotesIndexViewModel(
    NotesDashboardDto Dashboard,
    IReadOnlyList<NotebookItem> Notebooks,
    IReadOnlyList<PdfItem> Pdfs,
    IReadOnlyList<EligibleNotesGroup> Groups);

public sealed record NotesNotebookViewModel(
    NotebookDetails Details,
    IReadOnlyList<EligibleNotesGroup> Groups,
    IReadOnlyList<NotebookItem> Notebooks);

public sealed record NotesReaderViewModel(
    PdfItem Pdf,
    long? GroupDocumentShareId,
    int InitialPage);

public sealed record NotesBookViewModel(
    PdfItem Pdf,
    long? GroupDocumentShareId,
    IReadOnlyList<NotesBookPage> Pages);