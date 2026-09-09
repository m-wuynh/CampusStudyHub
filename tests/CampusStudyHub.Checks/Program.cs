using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CampusStudyHub.Data;
using CampusStudyHub.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<HubDb>().UseSqlite(connection).Options;
var factory = new Factory(options);
var auth = new TestAuth();
var service = new HubService(factory, auth);
await using (var db = new HubDb(options))
{
    await db.Database.EnsureCreatedAsync();
    db.Students.AddRange(new Student { Id = "a", Name = "An", University = "U", Faculty = "F", Cohort = "K" }, new Student { Id = "b", Name = "Bình", University = "U", Faculty = "F", Cohort = "K" }, new Student { Id = "admin", IsAdmin = true });
    await db.SaveChangesAsync();
}
var checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); checks++; }
async Task Denied(Func<Task> action, string label)
{
    try { await action(); } catch (UnauthorizedAccessException) { Check(true, label); return; }
    throw new Exception("FAIL: " + label);
}
auth.Id = "a";
await service.SaveSubject(new Subject { Name = "Web", Code = "WEB" });
var state = await service.Load(); var subject = state.Subjects.Single();
await service.SaveItem(new StudyItem { SubjectId = subject.Id, Title = "Private task" });
var taskId = (await service.Load()).Items.Single().Id;
auth.Id = "b";
Check((await service.Load()).Items.Count == 0, "Other student cannot read private tasks");
await Denied(() => service.Act(taskId, "done"), "Non-member cannot mutate task");
await service.JoinSubject(subject.InviteCode);
await Denied(() => service.Act(taskId, "done"), "Classmate cannot complete another student's task");
auth.Id = "a";
await service.Act(taskId, "done");
Check((await service.Load()).Items.Single().CompletedAt is not null, "Completing task records completion time");
await service.SaveItem(new StudyItem { SubjectId = subject.Id, Kind = "document", Title = "Shared notes", Url = "https://example.com/notes" });
var docId = (await service.Load()).Items.Single(x => x.Kind == "document").Id;
auth.Id = "b";
Check((await service.Load()).Items.Single().Id == docId, "Classmate can read shared document, not private deadline");
await service.Act(docId, "bookmark");
Check((await service.Load()).Bookmarks.Contains(docId), "Bookmark persists");
await service.Act(docId, "bookmark");
Check(!(await service.Load()).Bookmarks.Contains(docId), "Bookmark toggles off");
await service.Act(docId, "report", "Incorrect material");
await Denied(() => service.Moderate(1, "Hidden", true), "Student cannot moderate report");
auth.Id = "admin";
var reportId = (await service.Load()).Reports.Single().Id;
await service.Moderate(reportId, "Incorrect material confirmed", true);
auth.Id = "b";
Check((await service.Load()).Items.Count == 0, "Hidden document is excluded from member reads");
auth.Id = "admin";
await service.Moderate(reportId, "Restored after review", false);
auth.Id = "a";
await service.SaveItem(new StudyItem { SubjectId = subject.Id, Kind = "group", Title = "Study together", Capacity = 2 });
var groupId = (await service.Load()).Items.Single(x => x.Kind == "group").Id;
auth.Id = "b";
await service.Act(groupId, "join"); await service.Act(groupId, "join");
Check((await service.Load()).Requests.Count == 1, "Duplicate group requests do not create duplicate records");
auth.Id = "a";
await service.ReviewRequest(groupId, "b", true);
Check((await service.Load()).Requests.Single().Status == "accepted", "Owner approves group request");
try { await service.SaveItem(new StudyItem { SubjectId = subject.Id, Kind = "document", Title = "Bad URL", Url = "javascript:alert(1)" }); throw new Exception("Unsafe URL accepted"); }
catch (ValidationException) { Check(true, "Rejects unsafe URL schemes"); }
auth.Id = "admin";
await service.Suspend("b");
auth.Id = "b";
await Denied(async () => { await service.Load(); }, "Suspended user cannot use existing service session");
Check(!HubService.SafeUrl("//evil.example") && HubService.SafeUrl("https://example.com"), "Link validation rejects protocol-relative URLs");
Console.WriteLine($"All {checks} checks passed.");

sealed class Factory(DbContextOptions<HubDb> options) : IDbContextFactory<HubDb>
{
    public HubDb CreateDbContext() => new(options);
}
sealed class TestAuth : AuthenticationStateProvider
{
    public string Id { get; set; } = "a";
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Id) }, "test"))));
}
