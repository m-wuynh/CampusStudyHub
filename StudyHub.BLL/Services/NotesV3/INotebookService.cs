using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.Notebooks;

public interface INotebookService
{
    Task<NotebookScreenDto> GetScreenAsync(
        long notebookId, long? pageId, long? sectionId,
        CancellationToken ct = default);

    Task<long> CreateNotebookAsync(
        string title, long? groupId, CancellationToken ct = default);

    Task RenameNotebookAsync(
        long notebookId, string title, CancellationToken ct = default);

    Task DeleteNotebookAsync(
        long notebookId, CancellationToken ct = default);

    Task RestoreNotebookAsync(
        long notebookId, CancellationToken ct = default);

    Task<long> CreateSectionAsync(
        long notebookId, string title, CancellationToken ct = default);

    Task RenameSectionAsync(
        long sectionId, string title, CancellationToken ct = default);

    Task DeleteSectionAsync(
        long sectionId, CancellationToken ct = default);
}
