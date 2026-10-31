using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.Sharing;

public interface IGroupShareService
{
    Task<IReadOnlyList<EligibleNotesGroup>> ListEligibleGroupsAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<GroupPdfItem>> ListSharedPdfsAsync(
        long groupId,
        CancellationToken ct = default);

    Task<PdfShareResult> SharePdfAsync(
        long documentId,
        long groupId,
        CancellationToken ct = default);

    Task UnsharePdfAsync(
        long shareId,
        string versionToken,
        CancellationToken ct = default);

    Task<CopyNotebookResult> CopyNotebookAsync(
        CopyNotebookRequest request,
        CancellationToken ct = default);
}