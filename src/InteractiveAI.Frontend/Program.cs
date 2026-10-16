using InteractiveAI.Frontend.Config;
using InteractiveAI.Frontend.Services;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<BackendOptions>(builder.Configuration.GetSection("Backend"));
builder.Services.AddHttpClient<BackendClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var extTypes = new FileExtensionContentTypeProvider();
extTypes.Mappings[".task"] = "application/octet-stream";
extTypes.Mappings[".wasm"] = "application/wasm";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = extTypes });
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Chat}/{action=Index}/{id?}");

app.Run();
