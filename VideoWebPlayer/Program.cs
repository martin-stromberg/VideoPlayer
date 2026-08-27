using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using msTools.Updater;
using Serilog;
using System.Security.Cryptography;
using System.Threading;
using VideoWebPlayer.Client;
using VideoWebPlayer.Components;
using VideoWebPlayer.Components.Account;
using VideoWebPlayer.Data;
using VideoWebPlayer.Extensions;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Ensure log directory exists in the program/content root.
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "Logs"));

// Configure logging from appsettings.json (including per-day file rolling + retention).
builder.Host.UseSerilog((ctx, services, cfg) =>
	cfg.ReadFrom.Configuration(ctx.Configuration)
	   .ReadFrom.Services(services)
	   .Enrich.FromLogContext());

builder.AddVideoWebPlayerServices();
builder.AddVideoWebPlayerAutoUpdate();

var app = builder.Build();

// Ensure the auto-update package store directories (Updates, Updates/pending, Updates/staging) exist.
// Without this, the update script cannot be written and the installation fails silently.
app.Services.GetRequiredService<IAutoUpdatePackageStore>()
    .EnsureAsync(CancellationToken.None)
    .GetAwaiter()
    .GetResult();

app.MigrateDatabase();
app.UseVideoWebPlayer();

// Start UDP Discovery Listener (im Test nicht starten, damit E2E-Tests keinen fixen Port belegen)
if (!app.Environment.IsEnvironment("Testing"))
{
    var udpPort = 5001; // Discovery port
    var serverAddress = $"http://{app.Configuration["Host:Address"] ?? "localhost"}:{app.Configuration["Host:Port"] ?? "5000"}";
    var udpListener = new UdpDiscoveryListener(udpPort, serverAddress);
    udpListener.Start();
}

app.Run();
