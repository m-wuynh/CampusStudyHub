using StudyHub.BLL.DTOs.NotesV3;

namespace StudyHub.BLL.Services.NotesV3.Notebooks;

public interface INotebookService
{
    Task<IReadOnlyList<NotebookItem>> ListAsync(
        long? groupId,
        string? search,
        CancellationToken ct = default);

    Task<NotebookDetails> GetAsync(
        long notebookId,
        CancellationToken ct = default);

    Task<long> CreateNotebookAsync(
        string title,
        long? groupId,
        CancellationToken ct = default);

    Task RenameNotebookAsync(
        long notebookId,
        string title,
        CancellationToken ct = default);

    Task DeleteNotebookAsync(
        long notebookId,
        CancellationToken ct = default);

    Task<long> CreateSectionAsync(
        long notebookId,
        string title,
        CancellationToken ct = default);

    Task RenameSectionAsync(
        long sectionId,
        string title,
        CancellationToken ct = default);

    Task DeleteSectionAsync(
        long sectionId,
        CancellationToken ct = default);

    Task<long> CreatePageAsync(
        long sectionId,
        string title,
        CancellationToken ct = default);

    Task<PageDetails> GetPageAsync(
        long pageId,
        CancellationToken ct = default);

    Task<PageSaveResult> SavePersonalPageAsync(
        long pageId,
        PageSaveRequest request,
        CancellationToken ct = default);

    Task RenamePageAsync(
        long pageId,
        string title,
        string versionToken,
        CancellationToken ct = default);

    Task DeletePageAsync(
        long pageId,
        string versionToken,
        CancellationToken ct = default);

    Task<IReadOnlyList<PageItem>> ListTrashAsync(
        long notebookId,
        CancellationToken ct = default);

    Task RestorePageAsync(
        long pageId,
        string versionToken,
        CancellationToken ct = default);

    Task MovePageAsync(
        long pageId,
        long targetSectionId,
        string versionToken,
        CancellationToken ct = default);

    Task ReorderNotebooksAsync(
        long? groupId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default);

    Task ReorderSectionsAsync(
        long notebookId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default);

    Task ReorderPagesAsync(
        long sectionId,
        IReadOnlyList<long> orderedIds,
        CancellationToken ct = default);
}