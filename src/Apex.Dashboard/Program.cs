using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Apex.Dashboard;
using Apex.Dashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBase"] ?? "http://localhost:5000";

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<ApexApiClient>();
builder.Services.AddSingleton<ApexSignalRService>();

var app = builder.Build();

// Start SignalR connection eagerly so all pages share one connection
var signalR = app.Services.GetRequiredService<ApexSignalRService>();
await signalR.StartAsync();

await app.RunAsync();
