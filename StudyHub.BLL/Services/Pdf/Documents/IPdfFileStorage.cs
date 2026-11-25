namespace StudyHub.BLL.Services.Pdf.Documents;

public sealed record StoredPdf(
    string StorageKey,
    string FileName,
    long SizeBytes);

public interface IPdfFileStorage
{
    Task<StoredPdf> SaveAsync(
        Stream source,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
