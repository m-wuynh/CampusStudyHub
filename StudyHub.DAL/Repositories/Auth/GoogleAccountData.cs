namespace StudyHub.DAL.Repositories.Auth;

public sealed record GoogleAccountData(
    long UserId,
    string DisplayName,
    string? Email,
    string? AvatarUrl,
    string RoleCode,
    string Status);
