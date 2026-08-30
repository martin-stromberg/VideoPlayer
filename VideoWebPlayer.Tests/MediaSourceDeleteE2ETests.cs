using System.Collections.Generic;
using System.Security.Claims;
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
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end-Test mit Playwright: Ein Administrator löscht eine Quelle über die Oberfläche
/// und die Quelle verschwindet. Die Datenbank enthält dabei alle zugehörigen Entitäten.
/// </summary>
[Trait("Category", "E2E")]
public sealed class MediaSourceDeleteE2ETests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private bool _skipBrowser;
    private readonly List<string> _consoleMessages = [];

    public MediaSourceDeleteE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-delete-e2e-{Guid.NewGuid()}.db");
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
        _serverUrl = addressFeature!.Addresses.First().TrimEnd('/')!;

        await SeedDatabaseAsync();

        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            _context = await _browser.NewContextAsync();
            _page = await _context.NewPageAsync();
            _page.SetDefaultTimeout(120_000);

            _page.Console += (_, e) => _consoleMessages.Add($"[console] {e.Type}: {e.Text}");
            _page.PageError += (_, e) => _consoleMessages.Add($"[page-error] {e}");
            _page.Response += (_, e) =>
            {
                if (e.Status >= 400)
                    _consoleMessages.Add($"[response] {e.Status} {e.Url}");
            };
        }
        catch (PlaywrightException)
        {
            _skipBrowser = true;
            _playwright?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
            await _page.CloseAsync();
        if (_context is not null)
            await _context.CloseAsync();
        if (_browser is not null)
            await _browser.CloseAsync();
        _playwright?.Dispose();

        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Admin_Can_Delete_MediaSource_And_It_Disappears()
    {
        if (_skipBrowser)
            return;

        await _page.GotoAsync($"{_serverUrl}/Account/Login?ReturnUrl=admin%2Fmediasources");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await _page.FillAsync("#email", "admin@test.com");
        await _page.FillAsync("#password", "P@ssw0rd123!");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);

        var currentUrl = _page.Url;
        var heading = await _page.Locator("h1").TextContentAsync() ?? "(none)";

        Assert.True(
            currentUrl.Contains("/admin/mediasources"),
            $"After login URL: {currentUrl}, heading: {heading}");

        // Ensure the seeded source is displayed.
        var sourceRow = _page.Locator("table tbody tr");
        await sourceRow.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // Give Blazor Server enough time to switch from prerender to interactive.
        await _page.WaitForTimeoutAsync(3000);

        // Trigger delete on the only source.
        await _page.GetByRole(AriaRole.Button, new() { Name = "Löschen" }).ClickAsync();

        // Wait for the table to disappear and the "no sources" placeholder to appear.
        try
        {
            await _page.GetByText("Noch keine Quellen vorhanden.").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        }
        catch (TimeoutException)
        {
            var rowCount = await _page.Locator("table tbody tr").CountAsync();
            var error = "(none)";
            if (await _page.Locator(".alert-danger").CountAsync() > 0)
                error = await _page.Locator(".alert-danger").TextContentAsync() ?? "(empty)";

            var progressCount = await _page.Locator(".progress").CountAsync();
            var deleteButtons = await _page.GetByRole(AriaRole.Button, new() { Name = "Löschen" }).CountAsync();
            var bodyText = await _page.Locator("body").TextContentAsync() ?? string.Empty;
            var bodyPreview = bodyText.Length <= 1000 ? bodyText : bodyText[..1000];
            var recentConsole = _consoleMessages.Count <= 20 ? _consoleMessages : _consoleMessages[^20..];

            Assert.Fail($"Delete did not complete. Rows: {rowCount}, progress: {progressCount}, deleteButtons: {deleteButtons}, error: {error}, body: {bodyPreview}, console: {string.Join(" | ", recentConsole)}");
        }

        Assert.Equal(0, await _page.Locator("table").CountAsync());
    }

    private async Task SeedDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        // Admin user
        var admin = new ApplicationUser
        {
            UserName = "admin@test.com",
            Email = "admin@test.com",
            EmailConfirmed = true,
            IsAdmin = true
        };
        var createResult = await userManager.CreateAsync(admin, "P@ssw0rd123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        await userManager.AddClaimAsync(admin, new Claim("IsAdmin", "True"));

        // Media source with all related entities
        var source = new MediaSource
        {
            Name = "Playwright Test Source",
            Host = "127.0.0.1",
            Port = 22,
            Path = "/test",
            Username = "user",
            Password = "pass"
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();

        var collection = new MediaCollection
        {
            Name = "Test Collection",
            MediaSourceId = source.Id
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync();

        var mediaItem = new MediaItem
        {
            Name = "Test MediaItem",
            Path = "/test/item.mkv",
            MediaCollectionId = collection.Id
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync();

        var movieCollection = new MovieCollection
        {
            Name = "Test Movie Collection",
            MediaSourceId = source.Id
        };
        db.MovieCollections.Add(movieCollection);
        await db.SaveChangesAsync();

        var movie = new Movie
        {
            Name = "Test Movie",
            MediaSourceId = source.Id,
            MovieCollectionId = movieCollection.Id
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync();

        var genre = new Genre
        {
            Name = "Action",
            MediaSourceId = source.Id
        };
        db.Genres.Add(genre);
        await db.SaveChangesAsync();

        db.GenreNames.Add(new GenreName { GenreId = genre.Id, Name = "Action" });
        db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genre.Id });
        await db.SaveChangesAsync();

        var tvShow = new TVShow
        {
            Name = "Test TV Show",
            MediaSourceId = source.Id
        };
        db.TVShows.Add(tvShow);
        await db.SaveChangesAsync();

        var season = new TVShowSeason
        {
            Name = "Season 1",
            TVShowId = tvShow.Id
        };
        db.TVShowSeasons.Add(season);
        await db.SaveChangesAsync();

        var episode = new TVShowEpisode
        {
            Name = "Test Episode",
            TVShowSeasonId = season.Id
        };
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync();

        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = mediaItem.Id });
        db.TVShowEpisodeMediaItems.Add(new TVShowEpisodeMediaItem { TVShowEpisodeId = episode.Id, MediaItemId = mediaItem.Id });
        db.TVShowGenres.Add(new TVShowGenre { TVShowId = tvShow.Id, GenreId = genre.Id });
        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = admin.Id });
        await db.SaveChangesAsync();

        // Pictures including generated background for episode
        var posterPicture = new Picture
        {
            MediaItemId = mediaItem.Id,
            Type = "poster",
            Data = [0x01],
            ContentType = "image/png"
        };
        db.Pictures.Add(posterPicture);
        await db.SaveChangesAsync();

        var backgroundPicture = new Picture
        {
            MediaItemId = mediaItem.Id,
            EpisodeId = episode.Id,
            Type = "thumb",
            Data = [0x02],
            ContentType = "image/png",
            IsGeneratedBackground = true
        };
        db.Pictures.Add(backgroundPicture);
        await db.SaveChangesAsync();

        episode.GeneratedBackgroundPictureId = backgroundPicture.Id;
        db.TVShowEpisodes.Update(episode);
        await db.SaveChangesAsync();

        db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = admin.Id,
            MovieId = movie.Id,
            TVShowEpisodeId = episode.Id,
            Position = TimeSpan.Zero,
            UpdatedAt = DateTime.UtcNow
        });

        db.FavoriteEntries.Add(new FavoriteEntry
        {
            UserId = admin.Id,
            MovieId = movie.Id,
            TVShowEpisodeId = episode.Id,
            CreatedAt = DateTime.UtcNow
        });

        db.RecentEntries.Add(new RecentEntry
        {
            MediaSourceId = source.Id,
            MovieId = movie.Id,
            TVShowEpisodeId = episode.Id,
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
