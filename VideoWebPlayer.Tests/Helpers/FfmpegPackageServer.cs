using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Minimal Kestrel loopback host on <c>http://127.0.0.1:0</c> (free port from the OS — the
/// established project pattern from <see cref="PlaylistsE2ETestBase"/>; deliberately no
/// <c>HttpListener</c>, which can need http.sys URL reservations under Windows). Serves the
/// ffmpeg package fixtures so E2E tests can point <c>MediaProbe:DownloadSources:*</c> at it.
/// </summary>
internal sealed class FfmpegPackageServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private FfmpegPackageServer(WebApplication app, string baseUrl)
    {
        _app = app;
        BaseUrl = baseUrl;
    }

    public string BaseUrl { get; }

    /// <summary>URL serving the zip package (dummy <c>bin/ffmpeg(.exe)</c> entries).</summary>
    public string ZipUrl => $"{BaseUrl}/pkg.zip";

    /// <summary>URL serving the tar.xz package.</summary>
    public string TarXzUrl => $"{BaseUrl}/pkg.tar.xz";

    /// <summary>When set, the package endpoints answer 404 — the failure-path scenario.</summary>
    public bool FailWith404 { get; set; }

    /// <summary>
    /// When positive, the response body is written in two halves with this delay in between —
    /// keeps the download in <c>Running</c> long enough for the UI to show progress and
    /// disabled buttons deterministically.
    /// </summary>
    public int TransferDelayMilliseconds { get; set; }

    public static async Task<FfmpegPackageServer> StartAsync(byte[] zipPayload, byte[] tarXzPayload)
    {
        var builder = WebApplication.CreateSlimBuilder();
        // Explizites Listen auf IPv4-Loopback — UseUrls kann durch eine gesetzte
        // ASPNETCORE_URLS-Umgebungsvariable ueberstimmt werden; der Loopback-Port kommt vom OS.
        builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));
        var app = builder.Build();

        FfmpegPackageServer? self = null;
        app.MapGet("/pkg.zip", (HttpContext context) => ServeAsync(self!, zipPayload, "application/zip", context));
        app.MapGet("/pkg.tar.xz", (HttpContext context) => ServeAsync(self!, tarXzPayload, "application/octet-stream", context));

        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var baseUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
        self = new FfmpegPackageServer(app, baseUrl);
        return self;
    }

    private static async Task ServeAsync(FfmpegPackageServer self, byte[] payload, string contentType, HttpContext context)
    {
        if (self.FailWith404)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = contentType;
        context.Response.ContentLength = payload.Length;

        if (self.TransferDelayMilliseconds > 0 && payload.Length > 1)
        {
            var half = payload.Length / 2;
            await context.Response.Body.WriteAsync(payload.AsMemory(0, half));
            await context.Response.Body.FlushAsync();
            await Task.Delay(self.TransferDelayMilliseconds);
            await context.Response.Body.WriteAsync(payload.AsMemory(half));
            return;
        }

        await context.Response.Body.WriteAsync(payload);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
