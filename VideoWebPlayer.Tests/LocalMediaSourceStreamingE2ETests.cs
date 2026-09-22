using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// E2E-Test auf HTTP-Ebene: Streaming und Download einer Datei aus einer lokalen
/// Medienquelle über den echten Items-Endpunkt inklusive Zugriffskontrolle.
/// </summary>
[Trait("Category", "E2E")]
public sealed class LocalMediaSourceStreamingE2ETests : IDisposable
{
    private static readonly byte[] VideoBytes = { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };

    private readonly string _dbPath;
    private readonly string _rootDir;
    private readonly string _videoPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    public LocalMediaSourceStreamingE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-local-stream-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-local-stream-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
        _videoPath = Path.Combine(_rootDir, "movie.mp4");
        File.WriteAllBytes(_videoPath, VideoBytes);

        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        _factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
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

    [Fact]
    public async Task Stream_LocalMovie_ReturnsOkWithVideoContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (movieId, token) = await SeedMovieAsync(grantAccess: true);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/items/movie/{movieId}/stream?access_token={token}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("video/mp4", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(VideoBytes, bytes);
    }

    [Fact]
    public async Task Download_LocalMovie_ReturnsOkWithFileName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (movieId, token) = await SeedMovieAsync(grantAccess: true);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/items/movie/{movieId}/download?access_token={token}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("movie.mp4", response.Content.Headers.ContentDisposition?.FileName ?? string.Empty);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(VideoBytes, bytes);
    }

    [Fact]
    public async Task Stream_LocalMovie_WithoutSourceAccess_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var (movieId, token) = await SeedMovieAsync(grantAccess: false);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/items/movie/{movieId}/stream?access_token={token}", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(long MovieId, string Token)> SeedMovieAsync(bool grantAccess)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<AuthorizationTokenService>();

        var user = new ApplicationUser
        {
            UserName = $"viewer-{Guid.NewGuid():N}",
            Email = $"viewer-{Guid.NewGuid():N}@example.com",
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded);

        var source = new MediaSource
        {
            Name = "Lokale Streaming-Quelle",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory,
            Host = string.Empty,
            Port = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        if (grantAccess)
            db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = user.Id });

        var collection = new MediaCollection
        {
            Name = "root",
            Path = _rootDir,
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);

        var movieCollection = new MovieCollection { Name = "Collection", MediaSourceId = source.Id };
        db.MovieCollections.Add(movieCollection);
        await db.SaveChangesAsync(ct);

        var movie = new Movie { Name = "Local Movie", MediaSourceId = source.Id, MovieCollectionId = movieCollection.Id };
        db.Movies.Add(movie);

        var mediaItem = new MediaItem
        {
            Name = Path.GetFileName(_videoPath),
            Path = _videoPath,
            MediaCollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);

        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = mediaItem.Id });
        await db.SaveChangesAsync(ct);

        return (movie.Id, tokenService.CreateToken(user).token);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
        try { Directory.Delete(_rootDir, recursive: true); } catch { /* ignore */ }
    }
}
