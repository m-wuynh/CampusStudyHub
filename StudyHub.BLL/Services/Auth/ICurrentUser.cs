namespace StudyHub.BLL.Services.Auth;

public interface ICurrentUser
{
    long UserId { get; }
    string DisplayName { get; }
}
