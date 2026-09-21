namespace StudyHub.BLL.DTOs;

public sealed record LoginRequestDto(string Email, string Password, bool RememberMe);
public sealed record UserProfileDto(long Id, string DisplayName, string? Email, string RoleCode, string Status);
