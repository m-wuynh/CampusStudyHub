using Microsoft.EntityFrameworkCore;
using StudyHub.Web.Data;
using StudyHub.Web.Services.Flexcil.Annotations;
using StudyHub.Web.Services.Flexcil.Documents;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Razor Pages
builder.Services.AddRazorPages();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<
    IAnnotationService,
    AnnotationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
