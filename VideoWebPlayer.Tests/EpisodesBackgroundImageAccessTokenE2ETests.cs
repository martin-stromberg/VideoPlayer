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
/// E2E-Test für den access_token-Regressionsfix: Das im Header angezeigte, generierte
/// Episoden-Hintergrundbild muss über den in der Bild-URL angehängten access_token
/// abrufbar sein; ohne access_token muss der Zugriff verweigert werden.
/// </summary>
[Trait("Category", "E2E")]
public sealed class EpisodesBackgroundImageAccessTokenE2ETests : IDisposable
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    public EpisodesBackgroundImageAccessTokenE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-e2e-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

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
    public async Task GetBackgroundImage_WithAccessTokenQueryParameter_ReturnsGeneratedImage()
    {
        var (episodeId, token) = await CreateAuthenticatedEpisodeWithGeneratedBackgroundAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/episodes/{episodeId}/background-image?access_token={token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBackgroundImage_WithoutAccessToken_ReturnsUnauthorized()
    {
        var (episodeId, _) = await CreateAuthenticatedEpisodeWithGeneratedBackgroundAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/episodes/{episodeId}/background-image");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(long EpisodeId, string Token)> CreateAuthenticatedEpisodeWithGeneratedBackgroundAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<AuthorizationTokenService>();

        var user = new ApplicationUser
        {
            UserName = $"tester-{Guid.NewGuid():N}",
            Email = $"tester-{Guid.NewGuid():N}@example.com",
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded);

        var show = new TVShow { Name = "Testshow", CreatedAt = DateTime.UtcNow };
        db.TVShows.Add(show);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var season = new TVShowSeason { Name = "Staffel 01", TVShowId = show.Id, CreatedAt = DateTime.UtcNow };
        db.TVShowSeasons.Add(season);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var episode = new TVShowEpisode { Name = "Testepisode", Number = 1, TVShowSeasonId = season.Id, CreatedAt = DateTime.UtcNow };
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var source = new MediaSource { Name = "Source", Path = "/source", Host = "localhost", Port = 22, CreatedAt = DateTime.UtcNow };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collection = new MediaCollection { Name = "Collection", Path = "/source/collection", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mediaItem = new MediaItem { Name = "image.png", Path = "/source/collection/image.png", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var picture = new Picture { MediaItemId = mediaItem.Id, Type = "fanart", Data = new byte[] { 1, 2, 3 }, ContentType = "image/png", IsGeneratedBackground = true };
        db.Pictures.Add(picture);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        episode.GeneratedBackgroundPictureId = picture.Id;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var token = tokenService.CreateToken(user);
        return (episode.Id, token.token);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }
}
