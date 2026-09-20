using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Contracts;
using StudyHub.BLL.Repositories;
using StudyHub.BLL.Services;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
var studyHubConnection = builder.Configuration.GetConnectionString("StudyHub")
    ?? throw new InvalidOperationException("Thiếu connection string 'StudyHub'.");
builder.Services.AddDbContext<StudyHubDbContext>(options => options.UseSqlServer(studyHubConnection));
builder.Services.AddScoped<IStudyGroupRepository, SqlStudyGroupRepository>();
builder.Services.AddScoped<IStudyGroupService, StudyGroupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

var groupApi = app.MapGroup("/api/groups");

groupApi.MapGet("", (IStudyGroupService groups) => Results.Ok(groups.GetGroups()));

groupApi.MapGet("/{groupId}", (string groupId, IStudyGroupService groups) =>
{
    var group = groups.GetGroup(groupId);
    return group is null ? Results.NotFound() : Results.Ok(group);
});

groupApi.MapPost("/{groupId}/join", (string groupId, IStudyGroupService groups) =>
{
    var group = groups.Join(groupId);
    return group is null ? Results.NotFound() : Results.Ok(group);
});

groupApi.MapDelete("/{groupId}/join", (string groupId, IStudyGroupService groups) =>
{
    try
    {
        var group = groups.Leave(groupId);
        return group is null ? Results.NotFound() : Results.Ok(group);
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }
});

groupApi.MapPost("/{groupId}/messages", (string groupId, SendGroupMessageRequest request, IStudyGroupService groups) =>
{
    var content = request.Content?.Trim();
    if (string.IsNullOrWhiteSpace(content))
    {
        return Results.BadRequest(new { message = "Nội dung tin nhắn không được để trống." });
    }

    if (content.Length > 1000)
    {
        return Results.BadRequest(new { message = "Tin nhắn không được dài quá 1000 ký tự." });
    }

    var message = groups.AddMessage(groupId, content);
    return message is null
        ? Results.BadRequest(new { message = "Bạn cần tham gia nhóm trước khi gửi tin nhắn." })
        : Results.Ok(message);
});

groupApi.MapPost("", (CreateGroupRequest request, IStudyGroupService groups) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Subject))
    {
        return Results.BadRequest(new { message = "Tên nhóm và môn học là bắt buộc." });
    }

    if (request.Name.Trim().Length > 100 || (request.Description?.Length ?? 0) > 500)
    {
        return Results.BadRequest(new { message = "Tên nhóm hoặc mô tả vượt quá độ dài cho phép." });
    }

    var group = groups.CreateGroup(request);
    return Results.Created($"/api/groups/{group.Id}", group);
});

app.Run();
