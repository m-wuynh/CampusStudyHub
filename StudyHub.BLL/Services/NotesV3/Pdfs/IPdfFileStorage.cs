namespace StudyHub.BLL.Services.NotesV3.Pdfs;

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

    // Dùng để dọn file nếu lưu metadata SQL thất bại.
    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}