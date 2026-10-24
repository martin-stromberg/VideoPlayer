using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Controllers.InternalMediaStreamController"/>: the
/// internal stream endpoint serves a media item only with a valid issued token and honors
/// HTTP range requests — the properties the ffprobe HTTP probe path relies on.
/// </summary>
public sealed class InternalMediaStreamControllerTests : IDisposable
{
    private static readonly byte[] VideoBytes = { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 };

    private readonly string _dbPath;
    private readonly string _rootDir;
    private readonly WebApplicationFactory<global::Program> _factory;

    public InternalMediaStreamControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-internal-stream-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-internal-stream-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
        File.WriteAllBytes(Path.Combine(_rootDir, "clip.mp4"), VideoBytes);

        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        _factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
                builder.UseSetting("Jwt:Key", jwtKey);
                builder.UseSetting("Jwt:Issuer", "VideoWebPlayer.Tests");
                builder.UseSetting("Jwt:ApiToken", "test-api-token");
                // TestServer hat keine gebundenen Adressen — die Basis-URL wird explizit
                // konfiguriert, damit TryCreateProbeUri ein Token ausstellen kann.
                builder.UseSetting("MediaProbe:HttpProbeBaseUrl", "http://localhost");
                builder.ConfigureServices(services =>
                {
                    services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = null);
                });
            });
    }

    [Fact]
    public async Task Stream_InvalidToken_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await SetTvSenderEnabledAsync(true);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/internal/media-stream/no-such-token", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Stream_ValidToken_ReturnsFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var (requestUri, _) = await IssueProbeUriAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(requestUri, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("video/mp4", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(VideoBytes, await response.Content.ReadAsByteArrayAsync(ct));
    }

    [Fact]
    public async Task Stream_ValidToken_RangeRequest_Returns206PartialContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (requestUri, _) = await IssueProbeUriAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Range = new RangeHeaderValue(2, 4);
        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(VideoBytes.Skip(2).Take(3).ToArray(), bytes);
    }

    [Fact]
    public async Task Stream_TokenIsBoundToIssuedItem_UnknownAfterInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        var (requestUri, mediaItemId) = await IssueProbeUriAsync();
        var tokenService = _factory.Services.GetRequiredService<IInternalMediaStreamTokenService>();
        var token = requestUri.Split('/').Last();

        Assert.Equal(mediaItemId, tokenService.ResolveMediaItemId(token));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/internal/media-stream/other-token", ct)).StatusCode);
    }

    [Fact]
    public async Task Stream_FeatureDisabled_ValidToken_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (requestUri, _) = await IssueProbeUriAsync();
        await SetTvSenderEnabledAsync(false);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(requestUri, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Sets the TV-channel release switch <see cref="Setup.TvSenderEnabled"/> in the test
    /// database so the endpoint tests run against a defined flag state — the internal
    /// stream endpoint answers 404 while the feature is unreleased.
    /// </summary>
    private async Task SetTvSenderEnabledAsync(bool enabled)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ProgramSettingsService>();
        await settings.UpdateTvSenderEnabledAsync(enabled, ct);
    }

    /// <summary>
    /// Seeds a local media source with one media item backed by the temp file and issues a
    /// probe URI for it. Returns the request path and the media item id.
    /// </summary>
    private async Task<(string RequestUri, long MediaItemId)> IssueProbeUriAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await SetTvSenderEnabledAsync(true);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new MediaSource
        {
            Name = "Interne Quelle",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory,
            Host = string.Empty,
            Port = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);

        var collection = new MediaCollection
        {
            Name = "root",
            Path = _rootDir,
            MediaSource = source,
            CreatedAt = DateTime.UtcNow
        };

        var filePath = Path.Combine(_rootDir, "clip.mp4");
        var item = new MediaItem
        {
            Name = Path.GetFileName(filePath),
            Path = filePath,
            MediaCollection = collection,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync(ct);

        var tokenService = _factory.Services.GetRequiredService<IInternalMediaStreamTokenService>();
        var uri = tokenService.TryCreateProbeUri(item.Id);
        Assert.NotNull(uri);
        return (new Uri(uri).PathAndQuery, item.Id);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
        try { Directory.Delete(_rootDir, recursive: true); } catch { /* ignore */ }
    }
}
