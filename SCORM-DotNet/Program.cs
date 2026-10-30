using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SCORM_Implementation.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MVC Services
builder.Services.AddControllersWithViews();

// Register SCORM Service
builder.Services.AddScoped<IScormService, ScormService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

Console.WriteLine("============================================================");
Console.WriteLine("🚀 .NET C# SCORM Implementation & Testing Studio running at:");
Console.WriteLine("👉 http://127.0.0.1:5050");
Console.WriteLine("============================================================");

app.Run();

