using StudyHub.BLL.DTOs;

namespace StudyHub.BLL.Services.Auth;

public interface IAuthService
{
    Task<AuthResultDto> LoginWithGoogleAsync(
        GoogleLoginDto login,
        CancellationToken cancellationToken = default);

    Task<AuthResultDto> LoginForDevelopmentAsync(
        CancellationToken cancellationToken = default);
}
