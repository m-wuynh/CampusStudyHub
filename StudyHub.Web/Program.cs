using Microsoft.EntityFrameworkCore;
using StudyHub.BLL.Services.StudyGroups;
using StudyHub.DAL.Persistence;
using StudyHub.DAL.Repositories.Common;
using StudyHub.DAL.Repositories.StudyGroups;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var studyHubConnection = builder.Configuration.GetConnectionString("StudyHub")
    ?? throw new InvalidOperationException("Thiếu connection string 'StudyHub'.");

builder.Services.AddDbContext<StudyHubDbContext>(options => options.UseSqlServer(studyHubConnection));
builder.Services.AddScoped<IStudyGroupRepository, SqlStudyGroupRepository>();
builder.Services.AddScoped<IRepository, EfRepository>();
builder.Services.AddScoped<IStudyGroupService, StudyGroupService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
