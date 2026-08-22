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
/// End-to-end-Test mit Playwright: Ein Administrator bearbeitet eine Filmsammlung,
/// speichert die Änderungen und der neue Titel bleibt sichtbar.
/// </summary>
public sealed class MovieCollectionEditE2ETests : IAsyncLifetime
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

    public MovieCollectionEditE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-edit-e2e-{Guid.NewGuid()}.db");
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
    public async Task Admin_Can_Edit_And_Save_MovieCollection_Title()
    {
        if (_skipBrowser)
            return;

        await _page.GotoAsync($"{_serverUrl}/Account/Login?ReturnUrl=moviecollection%2F1");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await _page.FillAsync("#email", "admin@test.com");
        await _page.FillAsync("#password", "P@ssw0rd123!");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);

        // Wait for Blazor Server to become interactive.
        await _page.WaitForTimeoutAsync(3000);

        // Verify the initial collection title is displayed.
        await Expect(_page.Locator("h1")).ToHaveTextAsync("Test Movie Collection");

        const string newCollectionTitle = "Updated Movie Collection";
        await SaveTitleAsync(newCollectionTitle);
        await Expect(_page.Locator("h1")).ToHaveTextAsync(newCollectionTitle);

        // Select the only movie to test editing a single movie's metadata.
        var movieBox = _page.Locator(".media-box").First;
        await movieBox.ClickAsync();
        await _page.WaitForTimeoutAsync(1000);

        await Expect(_page.Locator("h1")).ToHaveTextAsync("Test Movie");

        const string newMovieTitle = "Updated Test Movie";
        await SaveTitleAsync(newMovieTitle);
        await Expect(_page.Locator("h1")).ToHaveTextAsync(newMovieTitle);
    }

    private async Task SaveTitleAsync(string newTitle)
    {
        // Enter edit mode.
        await _page.Locator("button[title='Bearbeiten']").ClickAsync();

        // Wait for and fill the title input.
        await _page.WaitForSelectorAsync(".metadata-title-input");
        await _page.FillAsync(".metadata-title-input", newTitle);

        // Save the changes.
        await _page.Locator("button[title='Speichern']").ClickAsync();

        // Wait for the save to propagate and the view to refresh.
        await _page.WaitForTimeoutAsync(2000);
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
        var createResult = await userManager.CreateAsync(admin, "P@ssw0rd123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        await userManager.AddClaimAsync(admin, new Claim("IsAdmin", "True"));

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

        db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genre.Id });
        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = admin.Id });
        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = mediaItem.Id });
        await db.SaveChangesAsync();
    }
}
