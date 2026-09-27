namespace StudyHub.DAL.Repositories.Auth;

public interface IGoogleAuthRepository
{
    Task<GoogleAccountData> FindOrCreateAsync(
        string subject,
        string displayName,
        string? email,
        string? avatarUrl,
        CancellationToken cancellationToken = default);

    Task<GoogleAccountData> FindOrCreateDevelopmentAsync(
        CancellationToken cancellationToken = default);
}
