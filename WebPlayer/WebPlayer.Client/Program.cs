using Ljbc1994.Blazor.IntersectionObserver;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebPlayer.Client;
using WebPlayer.Client.Services;
using WebPlayer.Data;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var config = builder.Configuration;

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();
builder.Services.AddIntersectionObserver();
builder.Services.AddHttpClient("My.ServerAPI", client =>
{
    var baseAddress = config.GetConnectionString("api");
    client.BaseAddress = new Uri(baseAddress); // oder deine API-URL
});
builder.Services.AddHttpClient("Own", client =>
{
    var baseAddress = config.GetConnectionString("own");
    if (!string.IsNullOrWhiteSpace(baseAddress))
        client.BaseAddress = new Uri(baseAddress); // oder deine API-URL
});
builder.Services.AddTransient<IAPIClient, APIClient>();
builder.Services.AddTransient<IServiceAPIClient, ServiceAPIClient>();
builder.Services.AddScoped<IMediaDirectoryAccessApi, MediaDirectoryAccessApi>();


await builder.Build().RunAsync();
