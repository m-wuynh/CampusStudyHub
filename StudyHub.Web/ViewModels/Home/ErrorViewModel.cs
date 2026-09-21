namespace StudyHub.Web.ViewModels.Home;

public sealed record ErrorViewModel(string? RequestId)
{
    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
}
