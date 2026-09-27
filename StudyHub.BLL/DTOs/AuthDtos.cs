namespace StudyHub.BLL.DTOs;

public sealed record LoginRequestDto(string Email, string Password, bool RememberMe);
public sealed record UserProfileDto(long Id, string DisplayName, string? Email, string RoleCode, string Status);
public sealed record GoogleLoginDto(string Subject, string DisplayName, string? Email, string? AvatarUrl);
public sealed record AuthResultDto(bool Succeeded, UserProfileDto? User, string? ErrorMessage)
{
    public static AuthResultDto Success(UserProfileDto user) => new(true, user, null);
    public static AuthResultDto Failure(string errorMessage) => new(false, null, errorMessage);
}
