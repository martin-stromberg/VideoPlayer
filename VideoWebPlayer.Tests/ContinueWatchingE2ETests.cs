using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
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

    [Fact]
    public async Task E2E_Playlist_CreateAndReportProgress_EntryCreated()
    {
        var (userId, token, movieId) = await CreateAuthenticatedUserAndMovieAsync();
        var playlistId = await CreatePlaylistAsync(userId, "Meine Playlist");

        await PostProgressAsync(token, "movie", movieId, 300, 2700, playlistId);
        var entry = await WaitForContinueWatchingEntryAsync(userId, movieId, playlistId, TimeSpan.FromSeconds(5));
        Assert.NotNull(entry);

        var list = await GetContinueWatchingListAsync(token);
        var item = Assert.Single(list);
        Assert.Equal(playlistId, item.GetProperty("playlistId").GetInt64());
        Assert.Equal("Meine Playlist", item.GetProperty("playlistName").GetString());
    }

    [Fact]
    public async Task E2E_MultipleEntriesPlaylist_AllThreeVariantsVisible()
    {
        var (userId, token, movieId) = await CreateAuthenticatedUserAndMovieAsync();
        var playlist1 = await CreatePlaylistAsync(userId, "Playlist 1");
        var playlist2 = await CreatePlaylistAsync(userId, "Playlist 2");

        await PostProgressAsync(token, "movie", movieId, 100, 2700, null);
        await WaitForContinueWatchingEntryAsync(userId, movieId, null, TimeSpan.FromSeconds(5));

        await PostProgressAsync(token, "movie", movieId, 200, 2700, playlist1);
        await WaitForContinueWatchingEntryAsync(userId, movieId, playlist1, TimeSpan.FromSeconds(5));

        await PostProgressAsync(token, "movie", movieId, 300, 2700, playlist2);
        await WaitForContinueWatchingEntryAsync(userId, movieId, playlist2, TimeSpan.FromSeconds(5));

        var list = await GetContinueWatchingListAsync(token);
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public async Task E2E_MarkWatchedPlaylist_RemovesAllVariants()
    {
        var (userId, token, movieId) = await CreateAuthenticatedUserAndMovieAsync();
        var playlist1 = await CreatePlaylistAsync(userId, "Playlist 1");
        var playlist2 = await CreatePlaylistAsync(userId, "Playlist 2");

        await PostProgressAsync(token, "movie", movieId, 100, 2700, null);
        await WaitForContinueWatchingEntryAsync(userId, movieId, null, TimeSpan.FromSeconds(5));
        await PostProgressAsync(token, "movie", movieId, 200, 2700, playlist1);
        await WaitForContinueWatchingEntryAsync(userId, movieId, playlist1, TimeSpan.FromSeconds(5));
        await PostProgressAsync(token, "movie", movieId, 300, 2700, playlist2);
        await WaitForContinueWatchingEntryAsync(userId, movieId, playlist2, TimeSpan.FromSeconds(5));

        Assert.Equal(3, (await GetContinueWatchingListAsync(token)).Count);

        // Fortschritt innerhalb der Gesehen-Schwelle (Duration - 10s) -> markiert global als gesehen,
        // entfernt ALLE ContinueWatchingEntry-Varianten unabhaengig von ihrer PlaylistId.
        await PostProgressAsync(token, "movie", movieId, 2690, 2700, playlist1);

        var allRemoved = await WaitUntilAsync(async () => (await GetContinueWatchingListAsync(token)).Count == 0, TimeSpan.FromSeconds(5));
        Assert.True(allRemoved, "Alle ContinueWatching-Varianten sollten nach der Gesehen-Markierung entfernt sein.");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.WatchedEntries.AnyAsync(x => x.UserId == userId && x.MovieId == movieId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task E2E_ResumeFromPlaylistEntry_PlaylistContextRecovered()
    {
        var (userId, token, movieId) = await CreateAuthenticatedUserAndMovieAsync();
        await GrantMediaSourceAccessAsync(userId);
        var playlistId = await CreatePlaylistAsync(userId, "Meine Playlist");
        var playlistEntryId = await CreatePlaylistEntryAsync(playlistId, movieId, MediaTypeValues.Movie);

        await PostProgressAsync(token, "movie", movieId, 300, 2700, playlistId);
        await WaitForContinueWatchingEntryAsync(userId, movieId, playlistId, TimeSpan.FromSeconds(5));

        var list = await GetContinueWatchingListAsync(token);
        var item = Assert.Single(list);
        Assert.Equal(playlistEntryId, item.GetProperty("playlistEntryId").GetInt64());

        // Wie ContinueWatchingList.razor beim Klick auf einen playlist-gebundenen Eintrag: mit der
        // rekonstruierten PlaylistEntryId den Playlist-Wiedergabestart aufrufen (identisch zu
        // PlaylistDetail.razor -> PlaylistClient.StartPlaylistAsync(playlistId, entryId)). Muss exakt
        // auf denselben Eintrag/dasselbe Medium zurueckfuehren, wodurch der PlaylistPlaybackContext an
        // der Ursprungsposition rekonstruiert wird, statt nur auf die Playlist-Uebersicht zu verweisen.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsync($"/api/playlists/{playlistId}/play?entryId={playlistEntryId}", null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        Assert.Equal(playlistId, root.GetProperty("playlistId").GetInt64());
        Assert.Equal(playlistEntryId, root.GetProperty("currentEntryId").GetInt64());
        Assert.Equal(movieId, root.GetProperty("mediaId").GetInt64());
        Assert.Equal("movie", root.GetProperty("mediaType").GetString());
    }

    [Fact]
    public async Task E2E_HideWithPlaylistId_OnlyRemovesMatching()
    {
        var (userId, token, movieId) = await CreateAuthenticatedUserAndMovieAsync();
        var playlist1 = await CreatePlaylistAsync(userId, "Playlist 1");
        var playlist2 = await CreatePlaylistAsync(userId, "Playlist 2");

        await PostProgressAsync(token, "movie", movieId, 100, 2700, null);
        await WaitForContinueWatchingEntryAsync(userId, movieId, null, TimeSpan.FromSeconds(5));
        await PostProgressAsync(token, "movie", movieId, 200, 2700, playlist1);
        await WaitForContinueWatchingEntryAsync(userId, movieId, playlist1, TimeSpan.FromSeconds(5));
        await PostProgressAsync(token, "movie", movieId, 300, 2700, playlist2);
        await WaitForContinueWatchingEntryAsync(userId, movieId, playlist2, TimeSpan.FromSeconds(5));

        Assert.Equal(3, (await GetContinueWatchingListAsync(token)).Count);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var payload = new { MediaType = "movie", MediaId = movieId, PlaylistId = playlist1 };
        var body = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/continue-watching/hide", body, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var list = await GetContinueWatchingListAsync(token);
        Assert.Equal(2, list.Count);
        Assert.DoesNotContain(list, e => e.GetProperty("playlistId").ValueKind != JsonValueKind.Null && e.GetProperty("playlistId").GetInt64() == playlist1);
        Assert.Contains(list, e => e.GetProperty("playlistId").ValueKind != JsonValueKind.Null && e.GetProperty("playlistId").GetInt64() == playlist2);
        Assert.Contains(list, e => e.GetProperty("playlistId").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task E2E_SkipWithPlaylistId_NextWithSamePlaylistId()
    {
        var (userId, token, showId) = await CreateAuthenticatedUserAndShowAsync(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null) }));
        var episode1Id = await GetEpisodeIdAsync(showId, "Staffel 01", 1);
        var episode2Id = await GetEpisodeIdAsync(showId, "Staffel 01", 2);
        var playlistId = await CreatePlaylistAsync(userId, "Meine Playlist");

        await PostProgressAsync(token, "episode", episode1Id, 300, 2700, playlistId);
        await WaitForContinueWatchingEpisodeEntryAsync(userId, episode1Id, playlistId, TimeSpan.FromSeconds(5));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var payload = new { MediaType = "episode", MediaId = episode1Id, PlaylistId = playlistId };
        var body = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/continue-watching/skip", body, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var replacement = await WaitForContinueWatchingEpisodeEntryAsync(userId, episode2Id, playlistId, TimeSpan.FromSeconds(5));
        Assert.NotNull(replacement);

        var list = await GetContinueWatchingListAsync(token);
        var item = Assert.Single(list);
        Assert.Equal(episode2Id, item.GetProperty("entry").GetProperty("id").GetInt64());
        Assert.Equal(playlistId, item.GetProperty("playlistId").GetInt64());
    }

    [Fact]
    public async Task E2E_PlaylistDeleted_EntryBecomesFree()
    {
        var (userId, token, movieId) = await CreateAuthenticatedUserAndMovieAsync();
        var playlistId = await CreatePlaylistAsync(userId, "Meine Playlist");

        await PostProgressAsync(token, "movie", movieId, 300, 2700, playlistId);
        await WaitForContinueWatchingEntryAsync(userId, movieId, playlistId, TimeSpan.FromSeconds(5));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deleteResponse = await client.DeleteAsync($"/api/playlists/{playlistId}", TestContext.Current.CancellationToken);
        deleteResponse.EnsureSuccessStatusCode();

        // ON DELETE SET NULL: der Eintrag bleibt bestehen, verliert aber seinen Playlist-Bezug und wird
        // damit faktisch zu einem Playlist-losen Eintrag.
        var freed = await WaitForContinueWatchingEntryAsync(userId, movieId, null, TimeSpan.FromSeconds(5));
        Assert.NotNull(freed);

        var list = await GetContinueWatchingListAsync(token);
        var item = Assert.Single(list);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("playlistId").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("playlistEntryId").ValueKind);
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

    private async Task<(string UserId, string Token, long MovieId)> CreateAuthenticatedUserAndMovieAsync()
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

        var collection = new MovieCollection { Name = "Collection", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var movie = new Movie { Name = "Movie", MovieCollectionId = collection.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var token = tokenService.CreateToken(user);
        return (user.Id, token.token, movie.Id);
    }

    private async Task<long> CreatePlaylistAsync(string userId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var playlist = new Playlist { UserId = userId, Name = name };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return playlist.Id;
    }

    private async Task<long> CreatePlaylistEntryAsync(long playlistId, long mediaId, string mediaType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entry = new PlaylistEntry { PlaylistId = playlistId, MediaId = mediaId, MediaType = mediaType };
        db.PlaylistEntries.Add(entry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry.Id;
    }

    /// <summary>
    /// Grants the user regular access to <c>MediaSourceId = 1</c> (used by <see cref="CreateAuthenticatedUserAndMovieAsync"/>),
    /// creating the referenced <see cref="MediaSource"/> first if it does not exist yet. Required for
    /// <c>PlaylistService.StartPlaylistAsync</c> to consider the requested entry accessible (see
    /// <see cref="PlaylistEntryAccessResolver.CheckEntryAccessible"/>), mirroring
    /// <c>PlaylistServiceTestBase.GrantMediaSourceAccessForUserAsync</c>.
    /// </summary>
    /// <param name="userId">The id of the user to grant media-source access to.</param>
    /// <param name="mediaSourceId">The id of the media source to grant access to (created if missing).</param>
    private async Task GrantMediaSourceAccessAsync(string userId, long mediaSourceId = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await db.MediaSources.AnyAsync(s => s.Id == mediaSourceId, TestContext.Current.CancellationToken))
        {
            db.MediaSources.Add(new MediaSource { Id = mediaSourceId, Name = "Test Source", Path = "/test", Host = "localhost", Port = 22 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = userId, MediaSourceId = mediaSourceId });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task PostProgressAsync(string token, string mediaType, long mediaId, long positionSeconds, long durationSeconds, long? playlistId)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            MediaType = mediaType,
            MediaId = mediaId,
            PositionSeconds = positionSeconds,
            DurationSeconds = durationSeconds,
            PlaylistId = playlistId
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/continue-watching/progress", content, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<ContinueWatchingEntry?> WaitForContinueWatchingEntryAsync(string userId, long movieId, long? playlistId, TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entry = await db.ContinueWatchingEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == movieId && x.PlaylistId == playlistId, TestContext.Current.CancellationToken);
            if (entry != null) return entry;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        return null;
    }

    private async Task<ContinueWatchingEntry?> WaitForContinueWatchingEpisodeEntryAsync(string userId, long episodeId, long? playlistId, TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entry = await db.ContinueWatchingEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.TVShowEpisodeId == episodeId && x.PlaylistId == playlistId, TestContext.Current.CancellationToken);
            if (entry != null) return entry;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        return null;
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (await condition())
                return true;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        return await condition();
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
        await service.ProcessBufferedEntryAsync(userId, null, episodeId, CompletedPosition, Duration, ct: TestContext.Current.CancellationToken);
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
