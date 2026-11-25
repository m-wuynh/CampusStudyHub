namespace StudyHub.BLL.Services.Pdf;

public sealed class PdfException(
    int statusCode,
    string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}