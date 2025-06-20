using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SyncNotes.Client;
using SyncNotes.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using Syncfusion.Blazor;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://syncnotes-api-bpdbcghychh7hahg.westeurope-01.azurewebsites.net/") });
builder.Services.AddLogging(); // Add this line to register logging services
builder.Services.AddScoped<NoteHubService>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddSyncfusionBlazor();
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NNaF1cWWhPYVFwWmFZfVtgdl9HY1ZVTWY/P1ZhSXxWdkNiUH5acXdRT2RfVkJ9XUs=");


builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<AuthService>();


var host = builder.Build();

// First initialize auth service to load token
var authService = host.Services.GetRequiredService<AuthService>();
await authService.InitializeAsync();

// No need to call this separately as it's now handled in AuthService.InitializeAsync
// var customAuthStateProvider = host.Services.GetRequiredService<CustomAuthStateProvider>();
// await customAuthStateProvider.LoadUserFromStorageAsync();

await host.RunAsync();
