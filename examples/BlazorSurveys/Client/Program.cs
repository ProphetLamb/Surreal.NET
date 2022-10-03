using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorSurveys.Client;
using BlazorSurveys.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(s => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddHttpClient<SurveyHttpClient>(c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddSingleton<HubConnection>(sp => {
    NavigationManager navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HubConnectionBuilder()
       .WithUrl(navigationManager.ToAbsoluteUri("/surveyhub"))
       .WithAutomaticReconnect()
       .Build();
});
await builder.Build().RunAsync();
