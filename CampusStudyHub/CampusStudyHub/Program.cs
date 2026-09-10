using System.Security.Claims;
using CampusStudyHub.Components;
using CampusStudyHub.Data;
using CampusStudyHub.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddScoped<HubService>();
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);
builder.Services.AddDbContextFactory<HubDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDirectory, "campus.db")}"));
var authentication = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login"; o.AccessDeniedPath = "/login";
        o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.Events.OnValidatePrincipal = async context =>
        {
            await using var db = await context.HttpContext.RequestServices.GetRequiredService<IDbContextFactory<HubDb>>().CreateDbContextAsync();
            var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = id is null ? null : await db.Students.FindAsync(id);
            if (user is null || user.Suspended) { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(); }
        };
    });
var googleConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientId"]) && !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientSecret"]);
if (googleConfigured)
{
    authentication.AddGoogle(o =>
    {
        o.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        o.ClaimActions.MapJsonKey("picture", "picture");
        o.Events.OnCreatingTicket = async context =>
        {
            var principal = context.Principal!;
            var googleId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var email = principal.FindFirstValue(ClaimTypes.Email) ?? "";
            var domains = builder.Configuration.GetSection("Authentication:AllowedDomains").Get<string[]>() ?? [];
            if (domains.Length > 0 && !domains.Contains(email.Split('@').Last(), StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Email ngoài tên miền được phép.");
            await using var db = await context.HttpContext.RequestServices.GetRequiredService<IDbContextFactory<HubDb>>().CreateDbContextAsync();
            var user = await db.Students.FindAsync(googleId);
            if (user is null) { user = new Student { Id = googleId }; db.Students.Add(user); }
            if (user.Suspended) throw new InvalidOperationException("Tài khoản đã bị khóa.");
            user.Name = principal.FindFirstValue(ClaimTypes.Name) ?? email;
            user.Email = email; user.Avatar = principal.FindFirstValue("picture") ?? "";
            user.IsAdmin = (builder.Configuration.GetSection("Authentication:AdminEmails").Get<string[]>() ?? []).Contains(email, StringComparer.OrdinalIgnoreCase);
            HubService.Log(db, user.Id, "user_login"); await db.SaveChangesAsync();
        };
        o.Events.OnRemoteFailure = context => { context.HandleResponse(); context.Response.Redirect("/login?error=oauth"); return Task.CompletedTask; };
    });
}
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error", createScopeForErrors: true); app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
await using (var scope = app.Services.CreateAsyncScope())
{
    await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<HubDb>>().CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}
app.MapGet("/auth/google", () => googleConfigured ? Results.Challenge(new AuthenticationProperties { RedirectUri = "/dashboard" }, ["Google"]) : Results.Redirect("/login?error=config"));
app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery csrf) =>
{
    await csrf.ValidateRequestAsync(context); await context.SignOutAsync(); return Results.LocalRedirect("/");
}).RequireAuthorization();
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Demo:Enabled"))
{
    app.MapPost("/auth/demo", async (HttpContext context, IAntiforgery csrf, IDbContextFactory<HubDb> factory) =>
    {
        await csrf.ValidateRequestAsync(context);
        var role = (await context.Request.ReadFormAsync())["role"].ToString();
        var id = role == "admin" ? "demo-admin" : role == "peer" ? "demo-peer" : "demo-student";
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Students.FindAsync(id);
        if (user is null)
        {
            user = new Student { Id = id, Name = id == "demo-admin" ? "Quản trị demo" : id == "demo-peer" ? "Minh Anh" : "Nguyễn An", Email = id + "@example.test", IsAdmin = id == "demo-admin", University = "Đại học Demo", Faculty = "Công nghệ thông tin", Cohort = "K2024" };
            db.Students.Add(user); await db.SaveChangesAsync();
        }
        if (user.Suspended) return Results.Redirect("/login?error=suspended");
        if (!await db.Subjects.AnyAsync(x => x.OwnerId == "demo-student") && id == "demo-student")
        {
            var subject = new Subject { OwnerId = id, Name = "Lập trình ứng dụng Web", Code = "WEB301", Lecturer = "Nguyễn Minh", ClassName = "CNTT • K2024", InviteCode = "CAMPUSDEMO" };
            db.Subjects.Add(subject); db.Memberships.Add(new() { Subject = subject, StudentId = id });
            db.Items.AddRange(new StudyItem { OwnerId = id, Subject = subject, Kind = "deadline", Title = "Hoàn thành thiết kế cơ sở dữ liệu", Notes = "Vẽ ERD và thống nhất schema với nhóm.", When = DateTime.Today.AddDays(2).AddHours(23) }, new StudyItem { OwnerId = id, Subject = subject, Kind = "schedule", Title = "Lập trình Web • Phòng B.204", When = DateTime.Today.AddDays(1).AddHours(9) });
        }
        var demoSubject = await db.Subjects.SingleOrDefaultAsync(x => x.InviteCode == "CAMPUSDEMO");
        if (id == "demo-peer" && demoSubject is not null && !await db.Memberships.AnyAsync(x => x.SubjectId == demoSubject.Id && x.StudentId == id))
            db.Memberships.Add(new Membership { SubjectId = demoSubject.Id, StudentId = id });
        HubService.Log(db, id, "user_login"); await db.SaveChangesAsync();
        await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Name, user.Name) }, CookieAuthenticationDefaults.AuthenticationScheme)));
        return Results.Redirect("/dashboard");
    });
}
app.MapGet("/documents/{id:int}/open", async (int id, bool? download, HttpContext context, IDbContextFactory<HubDb> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var item = await db.Items.SingleOrDefaultAsync(x => x.Id == id && x.Kind == "document" && !x.Hidden);
    if (item is null || !await db.Memberships.AnyAsync(x => x.StudentId == userId && x.SubjectId == item.SubjectId && !x.Subject.Archived)) return Results.NotFound();
    if (download == true && item.FilePath == "") return Results.BadRequest("Liên kết ngoài không có file để tải.");
    if (download == true) item.Downloads++; else item.Views++;
    HubService.Log(db, userId, download == true ? "document_downloaded" : "document_viewed"); await db.SaveChangesAsync();
    if (item.FilePath != "") return Results.File(Path.Combine(dataDirectory, "uploads", Path.GetFileName(item.FilePath)), "application/octet-stream", item.FileName);
    return HubService.SafeUrl(item.Url) ? Results.Redirect(item.Url) : Results.NotFound();
}).RequireAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
