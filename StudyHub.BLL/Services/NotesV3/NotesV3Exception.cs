namespace StudyHub.BLL.Services.NotesV3;

public sealed class NotesV3Exception(
    int statusCode,
    string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}