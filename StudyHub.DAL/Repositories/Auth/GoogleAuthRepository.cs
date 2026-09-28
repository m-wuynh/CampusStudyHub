using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;

namespace StudyHub.DAL.Repositories.Auth;

public sealed class GoogleAuthRepository(StudyHubDbContext context) : IGoogleAuthRepository
{
    public Task<GoogleAccountData> FindOrCreateAsync(
        string subject,
        string displayName,
        string? email,
        string? avatarUrl,
        CancellationToken cancellationToken = default) =>
        FindOrCreateAsync("Google", subject, displayName, email, avatarUrl, cancellationToken);

    public Task<GoogleAccountData> FindOrCreateDevelopmentAsync(
        CancellationToken cancellationToken = default) =>
        FindOrCreateAsync(
            "Development",
            "local-development-user",
            "Tài khoản phát triển",
            "developer@studyhub.local",
            null,
            cancellationToken);

    private async Task<GoogleAccountData> FindOrCreateAsync(
        string provider,
        string subject,
        string displayName,
        string? email,
        string? avatarUrl,
        CancellationToken cancellationToken)
    {
        var normalizedSubject = Limit(subject.Trim(), 255);
        var now = DateTime.UtcNow;

        var externalLogin = await context.ExternalLogins
            .Include(login => login.User)
            .SingleOrDefaultAsync(
                login => login.Provider == provider && login.ProviderSubject == normalizedSubject,
                cancellationToken);

        if (externalLogin is not null)
        {
            externalLogin.LastLoginAtUtc = now;
            externalLogin.User.DisplayName = Limit(displayName.Trim(), 100);
            externalLogin.User.Email = LimitNullable(email, 320);
            externalLogin.User.AvatarUrl = LimitNullable(avatarUrl, 2048);
            externalLogin.User.UpdatedAtUtc = now;
            await context.SaveChangesAsync(cancellationToken);
            return ToAccount(externalLogin.User);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var user = new User
        {
            DisplayName = Limit(displayName.Trim(), 100),
            Email = LimitNullable(email, 320),
            AvatarUrl = LimitNullable(avatarUrl, 2048),
            EducationLevel = "University",
            RoleCode = "Student",
            Status = "Active",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        context.ExternalLogins.Add(new ExternalLogin
        {
            Provider = provider,
            ProviderSubject = normalizedSubject,
            UserId = user.UserId,
            CreatedAtUtc = now,
            LastLoginAtUtc = now
        });

        context.UserSettings.Add(new UserSetting
        {
            UserId = user.UserId,
            Theme = "System",
            LanguageCode = "vi",
            TimeZoneId = "SE Asia Standard Time",
            EmailRemindersEnabled = true,
            InAppRemindersEnabled = true,
            DefaultReminderMinutes = 15,
            DailyStudyGoalMinutes = 30,
            UpdatedAtUtc = now
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToAccount(user);
    }

    private static GoogleAccountData ToAccount(User user) => new(
        user.UserId,
        user.DisplayName,
        user.Email,
        user.AvatarUrl,
        user.RoleCode,
        user.Status);

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? LimitNullable(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : Limit(normalized, maxLength);
    }
}
