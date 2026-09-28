namespace StudyHub.BLL.Services.Notes;

public sealed class NotesException(
    string message,
    int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}
