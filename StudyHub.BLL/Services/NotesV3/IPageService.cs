namespace StudyHub.BLL.Services.NotesV3.Pages;

public interface IPageService
{
    Task<long> CreateAsync(
        long sectionId, string title, CancellationToken ct = default);

    Task SaveTextAsync(
        long pageId, string text, string? versionToken,
        CancellationToken ct = default);

    Task RenameAsync(
        long pageId, string title, string? versionToken,
        CancellationToken ct = default);

    Task DeleteAsync(
        long pageId, string? versionToken,
        CancellationToken ct = default);
}
