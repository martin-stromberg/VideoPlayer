using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Stellt einen echten, per Playwright gesteuerten Browser gegen eine gehostete
/// <see cref="global::Program"/>-Instanz mit vorbelegten Favoriten bereit, damit
/// die MediaBox-Kontextmenü-Interaktionen als gerenderte Blazor-/Browser-Ereignisse
/// geprüft werden können.
/// </summary>
public abstract class MediaBoxContextMenuE2ETestBase : IAsyncLifetime
{
    protected const string TestUserEmail = "context-menu-tester@example.com";
    protected const string TestUserPassword = "P@ssw0rd123!";
    protected static readonly string[] FavoriteMovieNames = ["Film Eins", "Film Zwei", "Film Drei", "Film Vier"];

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private string _serverUrl = null!;

    protected IPage Page { get; private set; } = null!;
    protected bool SkipBrowser { get; private set; }

    protected MediaBoxContextMenuE2ETestBase()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-context-menu-e2e-{Guid.NewGuid()}.db");
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
        _serverUrl = addressFeature!.Addresses.First().TrimEnd('/');

        await SeedDatabaseAsync();

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

    protected async Task LoginAndNavigateToHomeAsync()
    {
        await Page.GotoAsync($"{_serverUrl}/Account/Login?ReturnUrl=%2F");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.FillAsync("#email", TestUserEmail);
        await Page.FillAsync("#password", TestUserPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.Load);

        var favoritesHeading = Page.Locator("#favorites-title");
        await favoritesHeading.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // Blazor Server braucht nach dem Prerender etwas Zeit, um interaktiv zu werden.
        await Page.WaitForTimeoutAsync(1500);
    }

    protected ILocator FavoriteCards => Page.Locator(".media-box-shell[data-card-key^='favorite-']");

    protected async Task<(double X, double Y)> GetCenterAsync(ILocator locator)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var box = await locator.BoundingBoxAsync()
            ?? throw new InvalidOperationException("Element hat keine Bounding Box.");
        return (box.X + (box.Width / 2), box.Y + (box.Height / 2));
    }

    private async Task SeedDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            UserName = TestUserEmail,
            Email = TestUserEmail,
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(user, TestUserPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException($"Testbenutzer konnte nicht erstellt werden: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

        var source = new MediaSource
        {
            Name = "Context Menu Test Source",
            Host = "127.0.0.1",
            Port = 22,
            Path = "/test",
            Username = "user",
            Password = "pass"
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();

        var collection = new MovieCollection
        {
            Name = "Context Menu Test Collection",
            MediaSourceId = source.Id
        };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync();

        foreach (var name in FavoriteMovieNames)
        {
            var movie = new Movie
            {
                Name = name,
                MediaSourceId = source.Id,
                MovieCollectionId = collection.Id
            };
            db.Movies.Add(movie);
            await db.SaveChangesAsync();

            db.FavoriteEntries.Add(new FavoriteEntry
            {
                UserId = user.Id,
                MovieId = movie.Id,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}
