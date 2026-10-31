using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.PageNotes;

public interface IPageNotesService
{
    Task<PdfPageNoteDetails> GetAsync(
        long documentVersionId,
        int physicalPageNumber,
        long? groupDocumentShareId = null,
        CancellationToken ct = default);

    Task<SavePdfPageNoteResult> SavePersonalAsync(
        long documentVersionId,
        int physicalPageNumber,
        SavePdfPageNoteRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<NotesBookPage>> ListBookPagesAsync(
        long documentVersionId,
        long? groupDocumentShareId = null,
        CancellationToken ct = default);
}