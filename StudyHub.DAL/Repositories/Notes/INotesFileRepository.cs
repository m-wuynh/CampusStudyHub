namespace StudyHub.DAL.Repositories.Notes;

public sealed record StoredNotePage(
    Guid Version,
    string Payload);

public interface INotesFileRepository
{
    Task<string> AddPdfAsync(
        Stream input,
        CancellationToken ct);

    Stream OpenPdf(string key);

    void DeletePdf(string key);

    Task<StoredNotePage?> ReadPageAsync(
        string key,
        int page,
        CancellationToken ct);

    Task<Guid?> SavePageAsync(
        string key,
        int page,
        Guid? expectedVersion,
        string payload,
        CancellationToken ct);
}
