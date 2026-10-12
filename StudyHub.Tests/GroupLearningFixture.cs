using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyHub.DAL.Entities;
using StudyHub.DAL.Persistence;
using Xunit;

namespace StudyHub.Tests;

public sealed class GroupLearningFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DatabaseName { get; } = "StudyHub_GroupLearningTests_" + Guid.NewGuid().ToString("N");
    public string Connection => new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STUDYHUB_TEST_SQL")
        ?? @"Server=.\SQLEXPRESS;Integrated Security=true;TrustServerCertificate=true") { InitialCatalog = DatabaseName }.ConnectionString;
    public long OwnerId, ModeratorId, MemberId, PeerId, PendingId, BannedId, OutsiderId, GroupId, OtherGroupId;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Never run reminder email jobs or use the development database during tests.
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<StudyHubDbContext>>();
            services.RemoveAll<StudyHubDbContext>();
            services.AddDbContext<StudyHubDbContext>(o => o.UseSqlServer(Connection));
            services.AddAuthentication(o => { o.DefaultAuthenticateScheme = "Test"; o.DefaultChallengeScheme = "Test"; })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            services.AddLogging(o => o.SetMinimumLevel(LogLevel.Error));
        });
    }

    public async Task InitializeAsync()
    {
        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();
        // Exercise the actual incremental migration against an existing schema.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE GroupAnswers DROP CONSTRAINT FK_GroupAnswers_GroupQuestions_QuestionId;
            DROP TABLE GroupSessionAttendances, GroupSubmissions, GroupWeeklyGoals;
            DROP TABLE GroupAssignmentAssignees;
            DROP TABLE GroupAssignments;
            DROP TABLE GroupStudySessions, GroupQuestions;
            DROP TABLE GroupAnswers;
            CREATE TABLE __EFMigrationsHistory (MigrationId nvarchar(150) NOT NULL PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL);
            """);
        var migrations = db.Database.GetMigrations().ToArray();
        var script = db.GetService<IMigrator>().GenerateScript(migrations[^2], migrations[^1], MigrationsSqlGenerationOptions.NoTransactions);
        foreach (var batch in Regex.Split(script, @"^GO\s*$", RegexOptions.Multiline).Where(x => !string.IsNullOrWhiteSpace(x)))
            await db.Database.ExecuteSqlRawAsync(batch);
        var users = new[] { "Owner", "Moderator", "Member", "Peer", "Pending", "Banned", "Outsider" }
            .Select(name => new User { DisplayName = name }).ToArray();
        db.Users.AddRange(users); await db.SaveChangesAsync();
        OwnerId = users[0].UserId; ModeratorId = users[1].UserId; MemberId = users[2].UserId;
        PeerId = users[3].UserId; PendingId = users[4].UserId; BannedId = users[5].UserId; OutsiderId = users[6].UserId;
        var group = new StudyGroup { OwnerUserId = OwnerId, GroupName = "Nhóm kiểm thử", Visibility = "Public", JoinMode = "Open", MeetingFormat = "Online" };
        var other = new StudyGroup { OwnerUserId = OwnerId, GroupName = "Nhóm khác", Visibility = "Public", JoinMode = "Open", MeetingFormat = "Online" };
        db.StudyGroups.AddRange(group, other); await db.SaveChangesAsync();
        GroupId = group.StudyGroupId; OtherGroupId = other.StudyGroupId;
        foreach (var u in users.Take(6)) db.GroupMembers.Add(new GroupMember { StudyGroupId = GroupId, UserId = u.UserId,
            MemberRole = u.UserId == ModeratorId ? "Moderator" : "Member",
            Status = u.UserId == PendingId ? "Pending" : u.UserId == BannedId ? "Banned" : "Active", JoinedAtUtc = DateTime.UtcNow });
        db.GroupMembers.Add(new GroupMember { StudyGroupId = OtherGroupId, UserId = OwnerId, MemberRole = "Moderator", Status = "Active", JoinedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
    public StudyHubDbContext NewDb() => new(new DbContextOptionsBuilder<StudyHubDbContext>().UseSqlServer(Connection).Options);
    public async Task<HttpClient> Client(long? userId, bool csrf = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        if (userId is not null) client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        if (csrf && userId is not null)
        {
            var response = await client.GetAsync($"/Groups/{GroupId}");
            var html = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, html);
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            client.DefaultRequestHeaders.Add("RequestVerificationToken", WebUtility.HtmlDecode(token));
        }
        return client;
    }
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        // The only database eligible for deletion is the random database created by this fixture.
        if (!DatabaseName.StartsWith("StudyHub_GroupLearningTests_", StringComparison.Ordinal)) throw new InvalidOperationException();
        await using var db = NewDb();
        await db.Database.EnsureDeletedAsync();
    }
}

public sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!long.TryParse(Request.Headers["X-Test-User"], out var id)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Name, "Test user")], "Test");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
    }
}
