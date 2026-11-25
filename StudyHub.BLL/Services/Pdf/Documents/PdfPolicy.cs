namespace StudyHub.BLL.Services.Pdf.Documents;

public static class PdfPolicy
{
    public const long MaxFileBytes = 25L * 1024 * 1024;
    public const long MaxUploadRequestBytes = 27L * 1024 * 1024;
    public const int MaxPageCount = 5_000;
    public const int MaxDocumentsPerUser = 200;
    public const long MaxStorageBytesPerUser = 2L * 1024 * 1024 * 1024;
    public const int MaxAnnotationsPerPage = 1_000;
}
