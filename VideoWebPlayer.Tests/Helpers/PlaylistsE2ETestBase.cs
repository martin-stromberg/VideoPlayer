using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Stellt einen echten, per Playwright gesteuerten Browser gegen eine gehostete
/// <see cref="global::Program"/>-Instanz mit vorbelegten Playlist-Testbenutzern bereit, damit
/// Playlist-Listenansicht und -Detailseite als gerenderte Blazor-/Browser-Ereignisse geprüft
/// werden können.
/// </summary>
public abstract class PlaylistsE2ETestBase : IAsyncLifetime
{
    protected const string UserAEmail = "playlist-user-a@test.com";
    protected const string UserBEmail = "playlist-user-b@test.com";
    protected const string Password = "P@ssw0rd123!";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;

    protected IPage Page { get; private set; } = null!;
    protected string ServerUrl { get; private set; } = null!;
    protected bool SkipBrowser { get; private set; }

    protected PlaylistsE2ETestBase()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-playlists-e2e-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        _factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseUrls("http://127.0.0.1:0");
                builder.UseStaticWebAssets();
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
                builder.UseSetting("Jwt:Key", jwtKey);
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
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        ServerUrl = addressFeature!.Addresses.First().TrimEnd('/');

        await SeedUsersAsync();

        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            _context = await _browser.NewContextAsync();
            Page = await _context.NewPageAsync();
            Page.SetDefaultTimeout(30_000);
        }
        catch (PlaywrightException)
        {
            SkipBrowser = true;
            _playwright?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Page is not null)
            await Page.CloseAsync();
        if (_context is not null)
            await _context.CloseAsync();
        if (_browser is not null)
            await _browser.CloseAsync();
        _playwright?.Dispose();

        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }

    protected async Task LoginAsync(string email)
    {
        await Page.GotoAsync($"{ServerUrl}/Account/Login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.FillAsync("#email", email);
        await Page.FillAsync("#password", Password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.Load);

        // Wait for Blazor Server to become interactive.
        await Page.WaitForTimeoutAsync(2000);
    }

    protected async Task<ILocator> CreatePlaylistViaUiAsync(string name, string? description = null)
    {
        await Page.GotoAsync($"{ServerUrl}/playlists");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Page.ClickAsync("#create-playlist-button");
        await Page.WaitForSelectorAsync("#playlist-name-input");
        await Page.FillAsync("#playlist-name-input", name);
        if (description is not null)
            await Page.FillAsync("#playlist-description-input", description);
        await Page.ClickAsync("#playlist-save-button");
        await Page.WaitForTimeoutAsync(1000);

        var row = Page.Locator($".playlist-row[data-playlist-name='{name}']");
        await Expect(row).ToBeVisibleAsync();
        return row;
    }

    /// <summary>
    /// Seeds a single movie directly in the database (bypassing the UI) and returns its id, for use
    /// as the target of the playlist "add media" form in E2E tests.
    /// </summary>
    protected async Task<long> SeedMovieAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sourceId = await EnsureMediaSourceIdAsync(db);

        var movie = new Movie { Name = name, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
        db.Movies.Add(movie);
        await db.SaveChangesAsync();
        return movie.Id;
    }

    /// <summary>
    /// Seeds a TV show with the given seasons and episode counts directly in the database and returns
    /// the show's id, for use as the target of the playlist "add media" form in E2E tests.
    /// </summary>
    protected async Task<long> SeedTvShowWithSeasonsAsync(string showName, params (string SeasonName, int EpisodeCount)[] seasons)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sourceId = await EnsureMediaSourceIdAsync(db);

        var show = new TVShow { Name = showName, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
        db.TVShows.Add(show);
        await db.SaveChangesAsync();

        foreach (var (seasonName, episodeCount) in seasons)
        {
            var season = new TVShowSeason { Name = seasonName, TVShowId = show.Id, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.TVShowSeasons.Add(season);
            await db.SaveChangesAsync();

            for (var number = 1; number <= episodeCount; number++)
            {
                db.TVShowEpisodes.Add(new TVShowEpisode
                {
                    Name = $"{seasonName} Episode {number}",
                    Number = number,
                    TVShowSeasonId = season.Id,
                    MediaSourceId = sourceId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        return show.Id;
    }

    /// <summary>
    /// Seeds the given number of movies directly in the database (bypassing the UI) and adds them
    /// as top-level entries to the playlist with the given name, for use in virtual-scrolling /
    /// lazy-loading E2E tests that need more entries than fit on a single page.
    /// </summary>
    protected async Task SeedMoviesIntoPlaylistAsync(string playlistName, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var playlist = await db.Playlists.FirstAsync(p => p.Name == playlistName);
        var sourceId = await EnsureMediaSourceIdAsync(db);

        for (var number = 1; number <= count; number++)
        {
            var movie = new Movie
            {
                Name = $"{playlistName} Film {number:D3}",
                MediaSourceId = sourceId,
                CreatedAt = DateTime.UtcNow,
                ReleaseDate = new DateTime(2000, 1, 1).AddDays(number)
            };
            db.Movies.Add(movie);
            await db.SaveChangesAsync();

            db.PlaylistEntries.Add(new PlaylistEntry
            {
                PlaylistId = playlist.Id,
                MediaType = MediaTypeValues.Movie,
                MediaId = movie.Id,
                AddedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a TV show directly in the database and adds it as a top-level entry to the playlist with
    /// the given name, for use in E2E tests covering real unlock/access behavior (only TV shows and
    /// movie collections are recognized by <see cref="VideoWebPlayer.Services.IUnlockedMediaService"/>).
    /// </summary>
    protected async Task<long> SeedTvShowIntoPlaylistAsync(string playlistName, string showName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var playlist = await db.Playlists.FirstAsync(p => p.Name == playlistName);
        var sourceId = await EnsureMediaSourceIdAsync(db);

        var show = new TVShow { Name = showName, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
        db.TVShows.Add(show);
        await db.SaveChangesAsync();

        db.PlaylistEntries.Add(new PlaylistEntry
        {
            PlaylistId = playlist.Id,
            MediaType = MediaTypeValues.TVShow,
            MediaId = show.Id,
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return show.Id;
    }

    /// <summary>
    /// Seeds a movie with a real poster picture directly in the database and adds it as a top-level
    /// entry to the playlist with the given name, for use in E2E tests covering the playlist image column.
    /// </summary>
    protected async Task<long> SeedMovieWithPosterIntoPlaylistAsync(string playlistName, string movieName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var playlist = await db.Playlists.FirstAsync(p => p.Name == playlistName);
        var sourceId = await EnsureMediaSourceIdAsync(db);

        // A real (if minimal) 1x1 transparent GIF is used here instead of arbitrary bytes: the browser
        // fetches the picture successfully either way, but only valid image data actually decodes and
        // renders, so arbitrary bytes would trigger the <img> element's onerror fallback despite the
        // HTTP request succeeding.
        var pixelGif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBTAA7");
        var picture = new Picture { Type = "poster", Data = pixelGif, ContentType = "image/gif" };
        db.Pictures.Add(picture);
        await db.SaveChangesAsync();

        var movie = new Movie { Name = movieName, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow, PosterPictureId = picture.Id };
        db.Movies.Add(movie);
        await db.SaveChangesAsync();

        db.PlaylistEntries.Add(new PlaylistEntry
        {
            PlaylistId = playlist.Id,
            MediaType = MediaTypeValues.Movie,
            MediaId = movie.Id,
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return movie.Id;
    }

    /// <summary>
    /// Grants the given user unlocked access to a movie collection or TV show by inserting an
    /// <see cref="UnlockedMediaEntry"/> directly in the database, for use in E2E tests that need to
    /// manipulate the real per-user unlock/access status of a playlist entry.
    /// </summary>
    protected async Task UnlockMediaForUserAsync(string userEmail, string mediaType, long mediaId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(userEmail)
            ?? throw new InvalidOperationException($"Benutzer {userEmail} wurde nicht gefunden.");

        db.UnlockedMediaEntries.Add(UnlockedMediaTestHelper.CreateUnlockedMediaEntry(user.Id, mediaType, mediaId));
        await db.SaveChangesAsync();
    }

    private static async Task<long> EnsureMediaSourceIdAsync(ApplicationDbContext db)
    {
        var existing = await db.MediaSources.FirstOrDefaultAsync();
        if (existing is not null)
            return existing.Id;

        var source = new MediaSource
        {
            Name = "E2E Test Source",
            Host = "127.0.0.1",
            Port = 22,
            Path = "/test",
            Username = "user",
            Password = "pass"
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();
        return source.Id;
    }

    private async Task SeedUsersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        var userA = new ApplicationUser
        {
            UserName = UserAEmail,
            Email = UserAEmail,
            EmailConfirmed = true
        };
        var createA = await userManager.CreateAsync(userA, Password);
        if (!createA.Succeeded)
            throw new InvalidOperationException($"Benutzer A konnte nicht erstellt werden: {string.Join(", ", createA.Errors.Select(e => e.Description))}");

        var userB = new ApplicationUser
        {
            UserName = UserBEmail,
            Email = UserBEmail,
            EmailConfirmed = true
        };
        var createB = await userManager.CreateAsync(userB, Password);
        if (!createB.Succeeded)
            throw new InvalidOperationException($"Benutzer B konnte nicht erstellt werden: {string.Join(", ", createB.Errors.Select(e => e.Description))}");
    }
}
