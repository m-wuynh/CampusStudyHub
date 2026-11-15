using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using StudyHub.BLL.DTOs;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.Web.Controllers.StudyGroups;

// Exercises real routing, Razor rendering, form binding and antiforgery without a database.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAuthentication("check").AddScheme<AuthenticationSchemeOptions, CheckAuth>("check", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(StudyGroupsController).Assembly);
var service = DispatchProxy.Create<IStudyGroupService, FakeGroups>();
var fake = (FakeGroups)(object)service;
builder.Services.AddSingleton(service);
await using var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
await app.StartAsync();
using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
int checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); checks++; }
async Task<string> Page(string path) {
    var response = await client.GetAsync(path);
    var body = await response.Content.ReadAsStringAsync();
    Check(response.StatusCode == HttpStatusCode.OK, "GET " + path + " renders HTML");
    Check(response.Content.Headers.ContentType?.MediaType == "text/html", "HTML content type");
    return WebUtility.HtmlDecode(body);
}
string Token(string html) => Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
async Task<HttpResponseMessage> Post(string path, string token, params (string Key, string Value)[] fields) =>
    await client.PostAsync(path, new FormUrlEncodedContent(fields.Select(f => new KeyValuePair<string,string>(f.Key,f.Value)).Append(new("__RequestVerificationToken",token))));
try {
    var index = await Page("/Groups?q=math");
    Check(index.Contains("Sample group") && fake.Search == "math", "server-rendered searchable group list");
    Check(index.Contains("action=\"/Groups/1/Join\""), "join form route");
    Check((await client.PostAsync("/Groups/1/Join", new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, "POST without antiforgery rejected");
    var form = await Page("/Groups/Create"); var token = Token(form);
    Check(token.Length > 0, "antiforgery token rendered");
    using var invalid = await Post("/Groups/Create",token,("Name","Keep my input"),("MaxMembers","invalid"));
    var invalidHtml = WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync());
    Check(invalid.StatusCode == HttpStatusCode.OK && invalidHtml.Contains("Keep my input") && fake.Created == null, "invalid form redisplays input without writing");
    using var created = await Post("/Groups/Create",token,("Name","New group"),("Subjects","Math"),("Subjects","Physics"),("MeetingFormat","Online"),("JoinMode","InviteOnly"));
    Check(created.StatusCode == HttpStatusCode.Redirect && created.Headers.Location?.ToString() == "/Groups/1", "create redirects to details");
    Check(fake.Created?.Subjects?.Count == 2 && fake.Created.IsPublic == false && fake.Created.MaxMembers == null, "form binding preserves subjects, privacy and unlimited capacity");
    var details = await Page("/Groups/1");
    Check(details.Contains("action=\"/Groups/1/Messages\"") && details.Contains("name=\"memberAction\""), "chat and member actions rendered as forms");
    var edit = await Page("/Groups/1/Edit");
    Check(edit.Contains("Sample group") && edit.Contains("value=\"Math\" checked"), "edit prepopulates existing group");
    fake.Owner = false;
    Check((await client.GetAsync("/Groups/1/Edit")).StatusCode == HttpStatusCode.Forbidden, "non-owner edit GET forbidden");
    Check((await Post("/Groups/1/Edit", token,("Name","Unauthorized"))).StatusCode == HttpStatusCode.Forbidden, "non-owner edit POST forbidden");
    fake.Owner = true;
    using var edited = await Post("/Groups/1/Edit",token,("Name","Updated"),("Subjects","Math"),("JoinMode","Open"),("MeetingFormat","Hybrid"),("MaxMembers","12"));
    Check(edited.StatusCode == HttpStatusCode.Redirect && fake.Updated?.MaxMembers == 12 && fake.Updated.IsPublic, "edit binds and redirects");
    foreach (var action in new[] { "Join", "Leave", "RequestUnban", "Messages", "Announcements", "Members/2", "Invites" }) {
        using var response = await Post("/Groups/1/" + action,token,("content","Hello"),("memberAction","approve"),("isPinned","true"));
        Check(response.StatusCode == HttpStatusCode.Redirect, action + " uses POST/redirect");
    }
    Check(fake.MemberAction == "approve" && fake.Pinned, "member action and checkbox binding");
    fake.FailJoin = true;
    using var failed = await Post("/Groups/1/Join",token);
    Check(failed.StatusCode == HttpStatusCode.Redirect && (await Page("/Groups/1")).Contains("Membership denied"), "business errors displayed after redirect");
    Check((await client.GetAsync("/api/groups")).StatusCode == HttpStatusCode.NotFound, "old group API removed");
    client.DefaultRequestHeaders.Add("X-Anonymous","true");
    Check((await client.GetAsync("/Groups")).StatusCode == HttpStatusCode.Unauthorized, "anonymous access rejected");
    Console.WriteLine($"{checks} checks passed.");
} finally { await app.StopAsync(); }

public class FakeGroups : DispatchProxy {
    public string? Search; public bool Owner = true; public bool FailJoin; public bool Pinned; public string? MemberAction;
    public CreateGroupRequest? Created; public UpdateGroupRequest? Updated;
    GroupDetailsResponse Group() => new("1",1,"Sample group","Math","subject-toan",[new("Math","subject-toan",false)],"Description","Goal","Online","Weekly","Rules",2,null,"Active","Owner",true,Owner,Owner,true,"Approval",[new("1","Owner",null,"Owner","Active",DateTimeOffset.UtcNow,null,true,false,null),new("2","Pending",null,"Member","Pending",DateTimeOffset.UtcNow,null,false,false,null)],[],[],[],false);
    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        switch(method!.Name) {
            case "GetSubjectOptionsAsync": return Task.FromResult<IReadOnlyList<GroupSubjectOptionResponse>>([new("Math",false),new("Physics",false)]);
            case "GetGroupsAsync": Search = (string?)args![0]; return Task.FromResult<IReadOnlyList<GroupSummaryResponse>>([new("1","Sample group","Math","subject-toan",[new("Math","subject-toan",false)],"Description","Goal","Online","Weekly",2,null,0,null,null,false,false,true,"Approval")]);
            case "GetGroupAsync": return Task.FromResult<GroupDetailsResponse?>(Group());
            case "CreateGroupAsync": Created = (CreateGroupRequest)args![0]!; return Task.FromResult(Group());
            case "UpdateGroupAsync": Updated = (UpdateGroupRequest)args![1]!; return Task.FromResult(Group());
            case "JoinAsync" when FailJoin: throw new StudyGroupException("denied","Membership denied",403);
            case "JoinAsync": case "LeaveOrWithdrawAsync": case "RequestUnbanAsync": return Task.FromResult(new GroupMembershipResponse("1","Active","Done"));
            case "AddMessageAsync": case "AddAnnouncementAsync":
                if (method.Name == "AddAnnouncementAsync") Pinned = (bool)args![2]!;
                return Task.FromResult(new GroupPostResponse("1","1","Owner",null,(string)args![1]!,"Message",false,DateTimeOffset.UtcNow,true));
            case "ManageMemberAsync": MemberAction = (string)args![2]!; return Task.CompletedTask;
            case "CreateInviteAsync": return Task.FromResult(new GroupInviteResponse("abc","1","Sample group","Math",DateTimeOffset.UtcNow.AddDays(7),20,0));
            default: throw new NotSupportedException(method.Name);
        }
    }
}
public class CheckAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder) {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Request.Headers.ContainsKey("X-Anonymous") ? AuthenticateResult.NoResult() : AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"1"),new Claim(ClaimTypes.Name,"Tester")],"check")),"check")));
}
