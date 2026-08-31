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
/// End-to-end-Test mit Playwright: Ein Administrator schaltet eine Serie und
/// eine Filmsammlung für einen anderen Benutzer frei. Anschließend kann sich
/// dieser Benutzer anmelden und beide Titel aufrufen.
/// </summary>
[Trait("Category", "E2E")]
public sealed class UnlockedMediaE2ETests : IAsyncLifetime
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

    public UnlockedMediaE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-unlock-e2e-{Guid.NewGuid()}.db");
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
    public async Task Admin_Can_Unlock_TVShow_And_MovieCollection_For_User()
    {
        if (_skipBrowser)
            return;

        await LoginAsync("admin@test.com");

        await UnlockForUserAsync("/tvshow/1", "regular@test.com");
        await UnlockForUserAsync("/moviecollection/1", "regular@test.com");

        await LoginAsync("regular@test.com");

        await _page.GotoAsync($"{_serverUrl}/tvshow/1");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        await Expect(_page.Locator("h1")).ToHaveTextAsync("Test TV Show");

        await _page.GotoAsync($"{_serverUrl}/moviecollection/1");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        // A single-movie collection opens the movie detail directly.
        await Expect(_page.Locator("h1")).ToHaveTextAsync("Test Movie");
    }

    private async Task LoginAsync(string email)
    {
        await _page.GotoAsync($"{_serverUrl}/Account/Login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await _page.FillAsync("#email", email);
        await _page.FillAsync("#password", "P@ssw0rd123!");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);

        // Wait for Blazor Server to become interactive.
        await _page.WaitForTimeoutAsync(3000);
    }

    private async Task UnlockForUserAsync(string relativeUrl, string userEmail)
    {
        await _page.GotoAsync($"{_serverUrl}{relativeUrl}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for Blazor Server to become fully interactive.
        await _page.WaitForTimeoutAsync(2000);

        await _page.Locator("button[title='Freigaben verwalten']").ClickAsync();
        await _page.WaitForSelectorAsync(".unlock-dialog");

        var userItem = _page.Locator("label.unlock-user-item", new() { HasText = userEmail });
        await userItem.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var checkbox = userItem.Locator("input[type='checkbox']");
        await checkbox.CheckAsync();

        await _page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        // Wait for the dialog to close and the button to reflect the unlocked state.
        await _page.WaitForTimeoutAsync(1000);
        await Expect(_page.Locator("button.unlock-btn")).ToContainTextAsync("🔓");
    }

    private async Task SeedDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        var admin = new ApplicationUser
        {
            UserName = "admin@test.com",
            Email = "admin@test.com",
            EmailConfirmed = true,
            IsAdmin = true
        };
        var adminCreate = await userManager.CreateAsync(admin, "P@ssw0rd123!");
        if (!adminCreate.Succeeded)
            throw new InvalidOperationException($"Admin creation failed: {string.Join(", ", adminCreate.Errors.Select(e => e.Description))}");
        await userManager.AddClaimAsync(admin, new Claim("IsAdmin", "True"));

        var regular = new ApplicationUser
        {
            UserName = "regular@test.com",
            Email = "regular@test.com",
            EmailConfirmed = true,
            IsAdmin = false
        };
        var regularCreate = await userManager.CreateAsync(regular, "P@ssw0rd123!");
        if (!regularCreate.Succeeded)
            throw new InvalidOperationException($"Regular user creation failed: {string.Join(", ", regularCreate.Errors.Select(e => e.Description))}");

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

        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = admin.Id });

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

        var tvShow = new TVShow
        {
            Name = "Test TV Show",
            MediaSourceId = source.Id
        };
        db.TVShows.Add(tvShow);
        await db.SaveChangesAsync();

        var season = new TVShowSeason
        {
            Name = "Staffel 1",
            TVShowId = tvShow.Id
        };
        db.TVShowSeasons.Add(season);
        await db.SaveChangesAsync();

        var episode = new TVShowEpisode
        {
            Name = "Test Episode",
            Number = 1,
            TVShowSeasonId = season.Id
        };
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync();

        db.TVShowEpisodeMediaItems.Add(new TVShowEpisodeMediaItem { TVShowEpisodeId = episode.Id, MediaItemId = mediaItem.Id });

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

        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = mediaItem.Id });

        await db.SaveChangesAsync();
    }
}
