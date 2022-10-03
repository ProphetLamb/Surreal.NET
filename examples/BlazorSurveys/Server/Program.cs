using BlazorSurveys.Server.Hubs;

using Microsoft.AspNetCore.ResponseCompression;
using SurrealDB.Abstractions;
using SurrealDB.Configuration;
using SurrealDB.Driver.Rpc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<IDatabase>(s => new DatabaseRpc(Config.Create()
   .WithEndpoint("127.0.0.1:8082")
   .WithDatabase("test").WithNamespace("test")
   .WithBasicAuth("root", "root")
   .WithRpc(insecure: true).Build()));
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddResponseCompression(o => {
        o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" });
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseResponseCompression();
app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();


app.MapRazorPages();
app.MapControllers();
app.MapHub<SurveyHub>("/surveyhub");
app.MapFallbackToFile("index.html");

app.Run();
