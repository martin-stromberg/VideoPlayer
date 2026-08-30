using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// E2E-Tests für die Ermittlung der nächsten Episode in der Continue-Watching-Funktionalität
/// (Happy Path, Episoden-Lücken, Staffelwechsel, Serienende).
/// </summary>
[Trait("Category", "E2E")]
public sealed class ContinueWatchingE2ETests : IDisposable
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private static readonly TimeSpan Duration = ContinueWatchingServiceTestBase.Duration;
    private static readonly TimeSpan CompletedPosition = ContinueWatchingServiceTestBase.CompletedPosition;

    public ContinueWatchingE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-e2e-cw-{Guid.NewGuid()}.db");
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
    public async Task HappyPath_EpisodeCompleted_NextEpisodeAppearsInContinueWatchingList()
    {
        var (userId, token, showId) = await CreateAuthenticatedUserAndShowAsync(
            ("Staffel 01", new (int, DateTime?)[] { (1, new DateTime(2020, 1, 1)), (2, new DateTime(2020, 1, 8)) }));

        var episode1Id = await GetEpisodeIdAsync(showId, "Staffel 01", 1);
        var episode2Id = await GetEpisodeIdAsync(showId, "Staffel 01", 2);

        await CompleteEpisodeAsync(userId, episode1Id);

        var next = await GetContinueWatchingEpisodeIdAsync(token);
        Assert.Equal(episode2Id, next);
    }

    [Fact]
    public async Task EpisodeGap_EpisodeCompleted_NextAvailableEpisodeAppearsInContinueWatchingList()
    {
        var (userId, token, showId) = await CreateAuthenticatedUserAndShowAsync(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null), (5, null) }));

        var episode2Id = await GetEpisodeIdAsync(showId, "Staffel 01", 2);
        var episode5Id = await GetEpisodeIdAsync(showId, "Staffel 01", 5);

        await CompleteEpisodeAsync(userId, episode2Id);

        var next = await GetContinueWatchingEpisodeIdAsync(token);
        Assert.Equal(episode5Id, next);
    }

    [Fact]
    public async Task SeasonTransition_LastEpisodeOfSeasonCompleted_FirstEpisodeOfNextSeasonAppears()
    {
        var (userId, token, showId) = await CreateAuthenticatedUserAndShowAsync(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null) }),
            ("Staffel 02", new (int, DateTime?)[] { (1, null) }));

        var season1Episode2Id = await GetEpisodeIdAsync(showId, "Staffel 01", 2);
        var season2Episode1Id = await GetEpisodeIdAsync(showId, "Staffel 02", 1);

        await CompleteEpisodeAsync(userId, season1Episode2Id);

        var next = await GetContinueWatchingEpisodeIdAsync(token);
        Assert.Equal(season2Episode1Id, next);
    }

    [Fact]
    public async Task SeriesEnd_LastEpisodeOfLastSeasonCompleted_NoContinueWatchingEntryCreated()
    {
        var (userId, token, showId) = await CreateAuthenticatedUserAndShowAsync(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null) }));

        var episode2Id = await GetEpisodeIdAsync(showId, "Staffel 01", 2);

        await CompleteEpisodeAsync(userId, episode2Id);

        var list = await GetContinueWatchingListAsync(token);
        Assert.Empty(list);
    }

    private async Task<(string UserId, string Token, long ShowId)> CreateAuthenticatedUserAndShowAsync(
        params (string Name, (int Number, DateTime? ReleaseDate)[] Episodes)[] seasons)
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

        var show = await TestHelpers.CreateTvShowWithSeasonsAsync(db, seasons);

        var token = tokenService.CreateToken(user);
        return (user.Id, token.token, show.Id);
    }

    private async Task<long> GetEpisodeIdAsync(long showId, string seasonName, int number)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var episodes = await db.TVShowEpisodes
            .Where(e => e.Number == number && e.TVShowSeason.Name == seasonName && e.TVShowSeason.TVShowId == showId)
            .ToListAsync(TestContext.Current.CancellationToken);
        return episodes.First().Id;
    }

    private async Task CompleteEpisodeAsync(string userId, long episodeId)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ContinueWatchingService>();
        await service.ProcessBufferedEntryAsync(userId, null, episodeId, CompletedPosition, Duration, TestContext.Current.CancellationToken);
    }

    private async Task<List<JsonElement>> GetContinueWatchingListAsync(string token)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/continue-watching", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);
        return document.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private async Task<long> GetContinueWatchingEpisodeIdAsync(string token)
    {
        var list = await GetContinueWatchingListAsync(token);
        var entry = Assert.Single(list);
        return entry.GetProperty("entry").GetProperty("id").GetInt64();
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }
}
