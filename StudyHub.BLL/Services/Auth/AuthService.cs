using StudyHub.BLL.DTOs;
using StudyHub.DAL.Repositories.Common;

namespace StudyHub.BLL.Services.Auth;

public sealed class AuthService(IRepository repository) : IAuthService
{
    public async Task<AuthResultDto> LoginWithGoogleAsync(
        GoogleLoginDto login,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login.Subject))
            return AuthResultDto.Failure("Google không trả về mã định danh tài khoản.");

        if (string.IsNullOrWhiteSpace(login.DisplayName))
            return AuthResultDto.Failure("Google không trả về tên hiển thị.");

        var account = await repository.GoogleAuth.FindOrCreateAsync(
            login.Subject,
            login.DisplayName,
            login.Email,
            login.AvatarUrl,
            cancellationToken);

        if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return AuthResultDto.Failure("Tài khoản Study Hub này đã bị khóa hoặc ngừng hoạt động.");

        return AuthResultDto.Success(new UserProfileDto(
            account.UserId,
            account.DisplayName,
            account.Email,
            account.RoleCode,
            account.Status));
    }
}
