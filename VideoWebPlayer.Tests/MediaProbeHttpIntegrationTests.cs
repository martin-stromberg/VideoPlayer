using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// System-level integration test for the HTTP probe path (N9): a real Kestrel host serves
/// the internal media-stream endpoint on loopback, and the real ffprobe binary probes a
/// media item through the issued token URI — proving range-capable seeking, token auth and
/// the self-call URL resolution end to end. Skipped when no ffmpeg/ffprobe binaries exist.
/// </summary>
[Trait("Category", "E2E")]
public sealed class MediaProbeHttpIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly string _rootDir;
    private readonly string _videoPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private string _serverUrl = null!;
    private bool _realVideoCreated;

    public MediaProbeHttpIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-http-probe-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-http-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
        _videoPath = Path.Combine(_rootDir, "clip.mp4");

        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        _factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
                builder.UseUrls("http://127.0.0.1:0");
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
                builder.UseSetting("Jwt:Key", jwtKey);
                builder.UseSetting("Jwt:Issuer", "VideoWebPlayer.Tests");
                builder.UseSetting("Jwt:ApiToken", "test-api-token");
                builder.ConfigureServices(services =>
                {
                    services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = null);
                });
            });
    }

    public async ValueTask InitializeAsync()
    {
        _factory.UseKestrel();
        _factory.StartServer();

        var server = _factory.Services.GetRequiredService<IServer>();
        _serverUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');

        await CreateTestVideoAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
        try { Directory.Delete(_rootDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task HttpProbe_RangeEndpoint_ReturnsDuration()
    {
        var ct = TestContext.Current.CancellationToken;
        var mediaItemId = await SeedMediaItemAsync();
        var tokenService = _factory.Services.GetRequiredService<IInternalMediaStreamTokenService>();
        var locator = _factory.Services.GetRequiredService<IFfprobeLocator>();
        var options = _factory.Services.GetRequiredService<IOptions<MediaProbeOptions>>();

        var probeUri = tokenService.TryCreateProbeUri(mediaItemId);
        Assert.NotNull(probeUri);
        Assert.StartsWith("http://127.0.0.1:", probeUri);

        if (locator.Resolve() is null || !_realVideoCreated)
            return; // ohne ffprobe-Binary bzw. echte Videodatei ist der Duration-Nachweis nicht führbar

        var probe = new FfprobeMediaDurationProbe(
            locator, options, NullLogger<FfprobeMediaDurationProbe>.Instance);

        var result = await probe.ProbeUriAsync(probeUri, ct);

        Assert.NotNull(result.Duration);
        Assert.InRange(result.Duration!.Value.TotalSeconds, 1.5, 3.5);
        Assert.False(result.UriUnsupported);
    }

    [Fact]
    public async Task InternalStream_RangeRequest_ReturnsPartialContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var mediaItemId = await SeedMediaItemAsync();
        var tokenService = _factory.Services.GetRequiredService<IInternalMediaStreamTokenService>();

        var probeUri = tokenService.TryCreateProbeUri(mediaItemId);
        Assert.NotNull(probeUri);

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, probeUri);
        request.Headers.Range = new RangeHeaderValue(0, 15);
        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(16, (await response.Content.ReadAsByteArrayAsync(ct)).Length);
    }

    /// <summary>
    /// Creates a ~2s test video with ffmpeg, or an arbitrary byte file when ffmpeg is
    /// unavailable — the range test does not need decodable media; the duration test skips
    /// itself when ffprobe cannot produce a duration anyway is handled by binary presence.
    /// </summary>
    private async Task CreateTestVideoAsync()
    {
        var ffmpeg = E2EBinaryLocator.Find("ffmpeg");
        if (ffmpeg is not null)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-y -v error -f lavfi -i testsrc=duration=2:size=128x96:rate=10 -pix_fmt yuv420p \"{_videoPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(startInfo)!;
            await process.WaitForExitAsync();
            if (process.ExitCode == 0 && File.Exists(_videoPath))
            {
                _realVideoCreated = true;
                return;
            }
        }

        if (!File.Exists(_videoPath))
            await File.WriteAllBytesAsync(_videoPath, new byte[4096]);
    }

    private async Task<long> SeedMediaItemAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Der interne Stream-Endpunkt folgt dem globalen Feature-Flag — die Tests laufen
        // mit freigegebenem Sender, sonst antwortet er 404 statt 200/206.
        var settings = scope.ServiceProvider.GetRequiredService<ProgramSettingsService>();
        await settings.UpdateTvSenderEnabledAsync(true, ct);

        var source = new MediaSource
        {
            Name = "HTTP-Probe-Quelle",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory,
            Host = string.Empty,
            Port = 0,
            CreatedAt = DateTime.UtcNow
        };
        var collection = new MediaCollection
        {
            Name = "root",
            Path = _rootDir,
            MediaSource = source,
            CreatedAt = DateTime.UtcNow
        };
        var item = new MediaItem
        {
            Name = Path.GetFileName(_videoPath),
            Path = _videoPath,
            MediaCollection = collection,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }
}
