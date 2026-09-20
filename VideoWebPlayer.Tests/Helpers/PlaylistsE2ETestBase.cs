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

    /// <summary>
    /// The service provider of the hosted application, for E2E tests that need to seed data the
    /// dedicated <c>Seed*</c> helpers do not cover (e.g. pictures of a specific size).
    /// </summary>
    protected IServiceProvider Services => _factory.Services;

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

    /// <summary>
    /// Adds a media entry via the <c>MediaSearchSelector</c> live-search UI: types the given search
    /// term into the search input, waits for the matching result tile to appear and clicks it, which
    /// invokes <c>PlaylistEntriesList.OnMediaSelectedAsync</c> and adds the entry. Shared by
    /// <see cref="global::VideoWebPlayer.Tests.PlaylistEntriesE2ETests"/> and
    /// <see cref="global::VideoWebPlayer.Tests.PlaylistMediaSearchE2ETests"/>, which both drive the
    /// same search-and-select flow with different fixture data.
    /// </summary>
    /// <param name="searchTerm">The search term to type into the live-search input.</param>
    /// <param name="mediaType">The media type of the expected result tile.</param>
    /// <param name="mediaId">The media id of the expected result tile.</param>
    protected async Task SelectSearchResultAsync(string searchTerm, string mediaType, long mediaId)
    {
        await ShowAddModeAsync();
        await Page.FillAsync(".media-search-input", searchTerm);
        var resultLocator = Page.Locator($".media-search-result[data-media-type='{mediaType}'][data-media-id='{mediaId}']");
        await resultLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await resultLocator.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    /// <summary>
    /// Switches the detail page to the "Titel hinzufügen" area (search to add titles) if it is not shown yet.
    /// An empty playlist starts in that mode, a non-empty one in the list mode.
    /// </summary>
    protected async Task ShowAddModeAsync()
    {
        var searchInput = Page.Locator(".media-search-input");
        if (await searchInput.CountAsync() > 0)
            return;

        await Page.ClickAsync("#playlist-mode-add-button");
        await searchInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
    }

    /// <summary>
    /// Switches the detail page to the "Titel der Playlist" area (the entry tiles) if it is not shown yet.
    /// </summary>
    protected async Task ShowEntriesAsync()
    {
        var toggle = Page.Locator("#playlist-mode-entries-button");
        if (await toggle.CountAsync() > 0 && await toggle.GetAttributeAsync("aria-pressed") != "true")
            await toggle.ClickAsync();

        // The tiles (or the empty state) render right after the switch.
        await Page.WaitForTimeoutAsync(300);
    }

    /// <summary>
    /// Selects the entry tile of the given media (click) - its information then shows in the header.
    /// </summary>
    /// <param name="mediaType">The media type of the entry.</param>
    /// <param name="mediaId">The media id of the entry.</param>
    protected async Task SelectEntryAsync(string mediaType, long mediaId)
    {
        await ShowEntriesAsync();
        await Page.ClickAsync($".playlist-entry-row[data-media-type='{mediaType}'][data-media-id='{mediaId}']");
        await Page.WaitForSelectorAsync("#playlist-detail-selected-entry");
    }

    /// <summary>
    /// Starts playlist playback at the given entry the way the detail page now offers it: select the tile, then
    /// click "Abspielen" in the header (the tiles no longer carry a play button).
    /// </summary>
    /// <param name="mediaType">The media type of the entry.</param>
    /// <param name="mediaId">The media id of the entry.</param>
    protected async Task PlayEntryFromHeaderAsync(string mediaType, long mediaId)
    {
        await SelectEntryAsync(mediaType, mediaId);
        await Page.ClickAsync("#playlist-detail-play-entry-button");
    }

    /// <summary>
    /// Like <see cref="PlayEntryFromHeaderAsync"/> for a playlist that has exactly one entry tile.
    /// </summary>
    protected async Task PlaySoleEntryFromHeaderAsync()
    {
        await ShowEntriesAsync();
        await Page.ClickAsync(".playlist-entry-row");
        await Page.WaitForSelectorAsync("#playlist-detail-selected-entry");
        await Page.ClickAsync("#playlist-detail-play-entry-button");
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
    /// Opens a new DI scope, resolves its <see cref="ApplicationDbContext"/> and the id of the single
    /// shared media source used by the <c>Seed*</c> helpers on this base class (creating it if needed),
    /// then runs <paramref name="action"/> against them before the scope is disposed. Centralizes the
    /// scope/db/source-id boilerplate that every seed helper previously repeated.
    /// </summary>
    /// <typeparam name="T">The result type produced by <paramref name="action"/>.</typeparam>
    /// <param name="long">The id of the single shared media source, passed to <paramref name="action"/>.</param>
    /// <param name="action">The callback to run against the scoped db context and media-source id.</param>
    /// <returns>The result produced by <paramref name="action"/>.</returns>
    private async Task<T> RunScopedAsync<T>(Func<ApplicationDbContext, long, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sourceId = await EnsureMediaSourceIdAsync(db);
        return await action(db, sourceId);
    }

    /// <summary>
    /// Same as <see cref="RunScopedAsync{T}"/> for actions without a return value.
    /// </summary>
    /// <param name="action">The callback to run against the scoped db context and media-source id.</param>
    private Task RunScopedAsync(Func<ApplicationDbContext, long, Task> action)
        => RunScopedAsync<object?>(async (db, sourceId) =>
        {
            await action(db, sourceId);
            return null;
        });

    /// <summary>
    /// Same as <see cref="RunScopedAsync{T}"/>, additionally loading the playlist with the given name
    /// before running <paramref name="action"/>, for the seed helpers that add entries to an existing
    /// playlist.
    /// </summary>
    /// <typeparam name="T">The result type produced by <paramref name="action"/>.</typeparam>
    /// <param name="playlistName">The name of the existing playlist to load before running <paramref name="action"/>.</param>
    /// <param name="long">The id of the single shared media source, passed to <paramref name="action"/>.</param>
    /// <param name="Playlist">The loaded playlist, passed to <paramref name="action"/>.</param>
    /// <param name="action">The callback to run against the scoped db context, media-source id and playlist.</param>
    /// <returns>The result produced by <paramref name="action"/>.</returns>
    private Task<T> RunScopedWithPlaylistAsync<T>(string playlistName, Func<ApplicationDbContext, long, Playlist, Task<T>> action)
        => RunScopedAsync(async (db, sourceId) =>
        {
            var playlist = await db.Playlists.FirstAsync(p => p.Name == playlistName);
            return await action(db, sourceId, playlist);
        });

    /// <summary>
    /// Same as <see cref="RunScopedWithPlaylistAsync{T}"/> for actions without a return value.
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist to load before running <paramref name="action"/>.</param>
    /// <param name="action">The callback to run against the scoped db context, media-source id and playlist.</param>
    private Task RunScopedWithPlaylistAsync(string playlistName, Func<ApplicationDbContext, long, Playlist, Task> action)
        => RunScopedAsync(async (db, sourceId) =>
        {
            var playlist = await db.Playlists.FirstAsync(p => p.Name == playlistName);
            await action(db, sourceId, playlist);
        });

    /// <summary>
    /// Resolves the <see cref="ApplicationUser"/> with the given email address via the
    /// <see cref="UserManager{TUser}"/> registered in the given scope, throwing if no such user exists.
    /// Centralizes the "resolve test user or fail loudly" step shared by the unlock/access helpers below.
    /// </summary>
    /// <param name="scopedServices">The DI scope's service provider to resolve the <see cref="UserManager{TUser}"/> from.</param>
    /// <param name="email">The email address of the user to resolve.</param>
    /// <returns>The resolved <see cref="ApplicationUser"/>.</returns>
    private static async Task<ApplicationUser> ResolveUserByEmailAsync(IServiceProvider scopedServices, string email)
    {
        var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Benutzer {email} wurde nicht gefunden.");
    }

    /// <summary>
    /// Seeds a single movie directly in the database (bypassing the UI) and returns its id, for use
    /// as the target of the playlist "add media" form in E2E tests.
    /// </summary>
    /// <param name="name">The name to give the created movie.</param>
    /// <returns>The id of the created movie.</returns>
    protected Task<long> SeedMovieAsync(string name)
        => RunScopedAsync(async (db, sourceId) =>
        {
            var movie = new Movie { Name = name, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.Movies.Add(movie);
            await db.SaveChangesAsync();
            return movie.Id;
        });

    /// <summary>
    /// Seeds a TV show with the given seasons and episode counts directly in the database and returns
    /// the show's id, for use as the target of the playlist "add media" form in E2E tests.
    /// </summary>
    /// <param name="showName">The name to give the created TV show.</param>
    /// <param name="SeasonName">The name of a season to create.</param>
    /// <param name="seasons">The (SeasonName, EpisodeCount) tuples describing the seasons and their episode counts.</param>
    /// <returns>The id of the created TV show.</returns>
    protected Task<long> SeedTvShowWithSeasonsAsync(string showName, params (string SeasonName, int EpisodeCount)[] seasons)
        => RunScopedAsync(async (db, sourceId) =>
        {
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
        });

    /// <summary>
    /// Seeds a movie collection directly in the database (bypassing the UI) and returns its id, for
    /// use as a media-search target in E2E tests that do not need the collection pre-added to a
    /// playlist.
    /// </summary>
    /// <param name="name">The name to give the created movie collection.</param>
    /// <returns>The id of the created movie collection.</returns>
    protected Task<long> SeedMovieCollectionAsync(string name)
        => RunScopedAsync(async (db, sourceId) =>
        {
            var collection = new MovieCollection { Name = name, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.MovieCollections.Add(collection);
            await db.SaveChangesAsync();
            return collection.Id;
        });

    /// <summary>
    /// Seeds a TV show with a single season and the given number of episodes directly in the database
    /// (bypassing the UI), for use as a media-search target in E2E tests covering season/episode
    /// selection in the search UI.
    /// </summary>
    /// <param name="showName">The name to give the created TV show.</param>
    /// <param name="seasonName">The name to give the created season.</param>
    /// <param name="episodeCount">The number of episodes to create in the season.</param>
    /// <returns>The ids of the created show, season and episodes.</returns>
    protected Task<(long ShowId, long SeasonId, long[] EpisodeIds)> SeedTvShowWithSingleSeasonAsync(string showName, string seasonName, int episodeCount)
        => RunScopedAsync(async (db, sourceId) =>
        {
            var show = new TVShow { Name = showName, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.TVShows.Add(show);
            await db.SaveChangesAsync();

            var season = new TVShowSeason { Name = seasonName, TVShowId = show.Id, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.TVShowSeasons.Add(season);
            await db.SaveChangesAsync();

            var episodeIds = new List<long>();
            for (var number = 1; number <= episodeCount; number++)
            {
                var episode = new TVShowEpisode
                {
                    Name = $"{seasonName} Episode {number}",
                    Number = number,
                    TVShowSeasonId = season.Id,
                    MediaSourceId = sourceId,
                    CreatedAt = DateTime.UtcNow
                };
                db.TVShowEpisodes.Add(episode);
                await db.SaveChangesAsync();
                episodeIds.Add(episode.Id);
            }

            return (show.Id, season.Id, episodeIds.ToArray());
        });

    /// <summary>
    /// Seeds the given number of movies directly in the database (bypassing the UI) and adds them
    /// as top-level entries to the playlist with the given name, for use in virtual-scrolling /
    /// lazy-loading E2E tests that need more entries than fit on a single page.
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist to add the movies to.</param>
    /// <param name="count">The number of movies to create and add.</param>
    protected Task SeedMoviesIntoPlaylistAsync(string playlistName, int count)
        => RunScopedWithPlaylistAsync(playlistName, async (db, sourceId, playlist) =>
        {
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
        });

    /// <summary>
    /// Seeds a TV show directly in the database and adds it as a top-level entry to the playlist with
    /// the given name, for use in E2E tests covering real unlock/access behavior (only TV shows and
    /// movie collections are recognized by <see cref="VideoWebPlayer.Services.IUnlockedMediaService"/>).
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist to add the TV show to.</param>
    /// <param name="showName">The name to give the created TV show.</param>
    /// <returns>The id of the created TV show.</returns>
    protected Task<long> SeedTvShowIntoPlaylistAsync(string playlistName, string showName)
        => RunScopedWithPlaylistAsync(playlistName, async (db, sourceId, playlist) =>
        {
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
        });

    /// <summary>
    /// Seeds a movie with a real poster picture directly in the database and adds it as a top-level
    /// entry to the playlist with the given name, for use in E2E tests covering the playlist image column.
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist to add the movie to.</param>
    /// <param name="movieName">The name to give the created movie.</param>
    /// <returns>The id of the created movie.</returns>
    protected Task<long> SeedMovieWithPosterIntoPlaylistAsync(string playlistName, string movieName)
        => RunScopedWithPlaylistAsync(playlistName, async (db, sourceId, playlist) =>
        {
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
        });

    /// <summary>
    /// Makes the user with the given email address an administrator (flag and <c>IsAdmin</c> claim), so the detail
    /// page offers the "öffentlich" toggle to them as the owner of a playlist.
    /// </summary>
    /// <param name="userEmail">The email address of the user to promote.</param>
    protected async Task MakeUserAdminAsync(string userEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await ResolveUserByEmailAsync(scope.ServiceProvider, userEmail);

        user.IsAdmin = true;
        await userManager.UpdateAsync(user);
        await userManager.AddClaimAsync(user, new System.Security.Claims.Claim("IsAdmin", "True"));
    }

    /// <summary>
    /// Sets (or, with <see langword="null"/>, clears) the cover of the playlist with the given name directly in the
    /// database, for E2E tests that need a specific image (e.g. extreme sizes) or cover state.
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist.</param>
    /// <param name="imageData">The PNG bytes of the cover, or <see langword="null"/> for no cover.</param>
    /// <param name="isUserUploaded">Whether the cover counts as uploaded by the user.</param>
    /// <returns>The id of the playlist.</returns>
    protected Task<long> SetPlaylistCoverAsync(string playlistName, byte[]? imageData, bool isUserUploaded)
        => RunScopedWithPlaylistAsync(playlistName, async (db, _, playlist) =>
        {
            if (imageData is null)
            {
                playlist.CoverPictureId = null;
                playlist.CoverPictureIsUserUploaded = false;
            }
            else
            {
                var picture = new Picture { Type = "cover", Data = imageData, ContentType = "image/png", PlaylistId = playlist.Id };
                db.Pictures.Add(picture);
                await db.SaveChangesAsync();
                playlist.CoverPictureId = picture.Id;
                playlist.CoverPictureIsUserUploaded = isUserUploaded;
            }

            await db.SaveChangesAsync();
            return playlist.Id;
        });

    /// <summary>
    /// Reads the current cover state of the playlist with the given name straight from the database.
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist.</param>
    /// <returns>The cover picture id (or <see langword="null"/>) and whether it counts as uploaded.</returns>
    protected async Task<(long? CoverPictureId, bool IsUserUploaded)> GetPlaylistCoverStateAsync(string playlistName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var playlist = await db.Playlists.AsNoTracking().FirstAsync(p => p.Name == playlistName);
        return (playlist.CoverPictureId, playlist.CoverPictureIsUserUploaded);
    }

    /// <summary>
    /// Seeds a movie with a poster picture, a plot and a release date into the playlist, for E2E tests of the
    /// entry information shown in the header.
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist.</param>
    /// <param name="movieName">The name of the movie.</param>
    /// <param name="posterPng">The PNG bytes of the poster.</param>
    /// <returns>The id of the created movie.</returns>
    protected Task<long> SeedDetailedMovieIntoPlaylistAsync(string playlistName, string movieName, byte[] posterPng)
        => RunScopedWithPlaylistAsync(playlistName, async (db, sourceId, playlist) =>
        {
            var picture = new Picture { Type = "poster", Data = posterPng, ContentType = "image/png" };
            db.Pictures.Add(picture);
            await db.SaveChangesAsync();

            var movie = new Movie
            {
                Name = movieName,
                MediaSourceId = sourceId,
                CreatedAt = DateTime.UtcNow,
                PosterPictureId = picture.Id,
                ReleaseDate = new DateTime(2018, 4, 12),
                Plot = $"Handlung von {movieName}."
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
            await db.SaveChangesAsync();
            return movie.Id;
        });

    /// <summary>
    /// Marks the playlist with the given name as public directly in the database (the UI only offers this to
    /// administrators), so E2E tests can show it to other users as a foreign public playlist.
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist to mark as public.</param>
    protected Task MakePlaylistPublicAsync(string playlistName)
        => RunScopedWithPlaylistAsync(playlistName, async (db, _, playlist) =>
        {
            playlist.IsPublic = true;
            await db.SaveChangesAsync();
        });

    /// <summary>
    /// Grants the given user unlocked access to a movie collection or TV show by inserting an
    /// <see cref="UnlockedMediaEntry"/> directly in the database, for use in E2E tests that need to
    /// manipulate the real per-user unlock/access status of a playlist entry.
    /// </summary>
    /// <param name="userEmail">The email address of the user to grant unlocked access to.</param>
    /// <param name="mediaType">The media type of the target entity (e.g. movie collection or TV show).</param>
    /// <param name="mediaId">The id of the target entity.</param>
    protected async Task UnlockMediaForUserAsync(string userEmail, string mediaType, long mediaId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await ResolveUserByEmailAsync(scope.ServiceProvider, userEmail);

        db.UnlockedMediaEntries.Add(UnlockedMediaTestHelper.CreateUnlockedMediaEntry(user.Id, mediaType, mediaId));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Grants the given user regular access to the single media source used by the other <c>Seed*</c>
    /// helpers on this base class, by inserting a <see cref="MediaSourceUser"/> entry directly in the
    /// database, for use in E2E tests that need to manipulate the real per-user source access status
    /// of a playlist entry.
    /// </summary>
    /// <param name="userEmail">The email address of the user to grant media-source access to.</param>
    protected async Task GrantMediaSourceAccessForUserAsync(string userEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await ResolveUserByEmailAsync(scope.ServiceProvider, userEmail);
        var sourceId = await EnsureMediaSourceIdAsync(db);

        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = user.Id, MediaSourceId = sourceId });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a movie directly in the database on a dedicated, never-granted media source (distinct from
    /// the single shared source the other <c>Seed*</c> helpers use) and adds it as a top-level entry to
    /// the playlist with the given name, for use in E2E tests that need a playlist entry which stays
    /// locked regardless of <see cref="GrantMediaSourceAccessForUserAsync"/> being called for the shared
    /// source (e.g. playback-navigation "skip locked entries" tests).
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist to add the movie to.</param>
    /// <param name="movieName">The name to give the created movie.</param>
    /// <returns>The id of the created movie.</returns>
    protected Task<long> SeedLockedMovieIntoPlaylistAsync(string playlistName, string movieName)
        => RunScopedWithPlaylistAsync(playlistName, async (db, _, playlist) =>
        {
            var lockedSource = new MediaSource { Name = $"Locked Source {Guid.NewGuid()}", Host = "127.0.0.1", Port = 22, Path = "/locked" };
            db.MediaSources.Add(lockedSource);
            await db.SaveChangesAsync();

            var movie = new Movie { Name = movieName, MediaSourceId = lockedSource.Id, CreatedAt = DateTime.UtcNow };
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
        });

    /// <summary>
    /// Seeds a movie belonging to the given movie collection directly in the database and adds the
    /// movie as a top-level entry to the playlist with the given name, for use in E2E tests covering
    /// the movie-to-collection unlock hierarchy resolution.
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist to add the movie to.</param>
    /// <param name="collectionName">The name to give the created movie collection.</param>
    /// <param name="movieName">The name to give the created movie.</param>
    /// <returns>The ids of the created movie and its movie collection.</returns>
    protected Task<(long MovieId, long CollectionId)> SeedMovieInCollectionIntoPlaylistAsync(string playlistName, string collectionName, string movieName)
        => RunScopedWithPlaylistAsync(playlistName, async (db, sourceId, playlist) =>
        {
            var collection = new MovieCollection { Name = collectionName, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.MovieCollections.Add(collection);
            await db.SaveChangesAsync();

            var movie = new Movie { Name = movieName, MediaSourceId = sourceId, MovieCollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
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

            return (movie.Id, collection.Id);
        });

    /// <summary>
    /// Seeds a TV show with a single season and episode directly in the database and adds the episode
    /// as a top-level entry to the playlist with the given name, for use in E2E tests covering the
    /// episode-to-show unlock hierarchy resolution.
    /// </summary>
    /// <param name="playlistName">The name of the existing playlist to add the episode to.</param>
    /// <param name="showName">The name to give the created TV show.</param>
    /// <returns>The ids of the created episode and its TV show.</returns>
    protected Task<(long EpisodeId, long ShowId)> SeedTvShowEpisodeIntoPlaylistAsync(string playlistName, string showName)
        => RunScopedWithPlaylistAsync(playlistName, async (db, sourceId, playlist) =>
        {
            var show = new TVShow { Name = showName, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.TVShows.Add(show);
            await db.SaveChangesAsync();

            var season = new TVShowSeason { Name = "Staffel 1", TVShowId = show.Id, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.TVShowSeasons.Add(season);
            await db.SaveChangesAsync();

            var episode = new TVShowEpisode { Name = "Episode 1", Number = 1, TVShowSeasonId = season.Id, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.TVShowEpisodes.Add(episode);
            await db.SaveChangesAsync();

            db.PlaylistEntries.Add(new PlaylistEntry
            {
                PlaylistId = playlist.Id,
                MediaType = MediaTypeValues.TVShowEpisode,
                MediaId = episode.Id,
                AddedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            return (episode.Id, show.Id);
        });

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
