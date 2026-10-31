using StudyHub.DAL.Entities.NotesV3;

namespace StudyHub.BLL.Services.NotesV3;

public sealed record NotesV3PageAccess(
    NotesV3Page Page,
    bool IsGroup,
    long? StudyGroupId,
    long? GroupDocumentShareId = null);

public interface INotesV3AccessService
{
    Task RequireGroupAsync(
        long groupId,
        CancellationToken cancellationToken = default);

    Task<NotesV3Notebook> RequireNotebookAsync(
        long notebookId,
        CancellationToken cancellationToken = default);

    Task<NotesV3Section> RequireSectionAsync(
        long sectionId,
        CancellationToken cancellationToken = default);

    Task<NotesV3PageAccess> RequirePageAsync(
        long pageId,
        bool includeDeleted = false,
        bool forWrite = false,
        CancellationToken cancellationToken = default);

    Task<NotesV3Document> RequireOwnedPdfAsync(
        long documentId,
        bool forWrite = false,
        CancellationToken cancellationToken = default);

    Task<NotesV3Document> RequirePdfAsync(
        long documentVersionId,
        long? groupDocumentShareId = null,
        bool forWrite = false,
        CancellationToken cancellationToken = default);
}