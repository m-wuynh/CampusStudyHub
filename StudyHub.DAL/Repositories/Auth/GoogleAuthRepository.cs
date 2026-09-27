using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;

namespace StudyHub.DAL.Repositories.Auth;

public sealed class GoogleAuthRepository(StudyHubDbContext context) : IGoogleAuthRepository
{
    private const string Provider = "Google";

    public async Task<GoogleAccountData> FindOrCreateAsync(
        string subject,
        string displayName,
        string? email,
        string? avatarUrl,
        CancellationToken cancellationToken = default)
    {
        var normalizedSubject = Limit(subject.Trim(), 255);
        var now = DateTime.UtcNow;

        var externalLogin = await context.ExternalLogins
            .Include(login => login.User)
            .SingleOrDefaultAsync(
                login => login.Provider == Provider && login.ProviderSubject == normalizedSubject,
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
            Provider = Provider,
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
