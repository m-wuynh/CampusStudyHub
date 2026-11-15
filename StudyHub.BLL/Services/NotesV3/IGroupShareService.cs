namespace StudyHub.BLL.Services.NotesV3.Sharing;

public interface IGroupShareService
{
    Task SharePdfAsync(
        long documentId, long groupId, CancellationToken ct = default);
}
