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
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// E2E-Test mit Playwright: Eine lokale Medienquelle mit Altersfreigaben in den NFOs
/// wird gescannt, die Badges erscheinen in den Detailansichten (Film, Serie, vererbte
/// Staffel-/Episoden-Anzeige) und ein Admin pflegt die Freigabe über den Editiermodus.
/// </summary>
[Trait("Category", "E2E")]
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class AgeRatingE2ETests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly string _rootDir;
    private readonly WebApplicationFactory<global::Program> _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private bool _skipBrowser;
    private long _ratedCollectionId;
    private long _editCollectionId;
    private long _showId;

    public AgeRatingE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-agerating-e2e-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-agerating-e2e-{Guid.NewGuid():N}");
        var movieDir = Path.Combine(_rootDir, "RatedMovie");
        var editDir = Path.Combine(_rootDir, "EditMovie");
        var showDir = Path.Combine(_rootDir, "RatedShow");
        Directory.CreateDirectory(movieDir);
        Directory.CreateDirectory(editDir);
        Directory.CreateDirectory(showDir);

        File.WriteAllText(Path.Combine(movieDir, "rated.mkv"), "video");
        File.WriteAllText(Path.Combine(movieDir, "rated.nfo"),
            "<movie><title>Rated Movie</title><mpaa>FSK 16</mpaa></movie>");
        File.WriteAllText(Path.Combine(editDir, "edit.mkv"), "video");
        File.WriteAllText(Path.Combine(editDir, "edit.nfo"),
            "<movie><title>Edit Movie</title></movie>");
        File.WriteAllText(Path.Combine(showDir, "episode01.mkv"), "video");
        File.WriteAllText(Path.Combine(showDir, "tvshow.nfo"),
            "<tvshow><title>Rated Show</title><certification>Germany:12</certification></tvshow>");
        File.WriteAllText(Path.Combine(showDir, "episode01.nfo"),
            "<episodedetails><title>Pilot</title><season>1</season><episode>1</episode></episodedetails>");

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

        await SeedAndScanAsync();

        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            _context = await _browser.NewContextAsync();
            _page = await _context.NewPageAsync();
            _page.SetDefaultTimeout(120_000);
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
        try { Directory.Delete(_rootDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task MovieNfoRating_ShowsBadgeOnMovieDetails()
    {
        if (_skipBrowser)
            return;

        await LoginAndGotoAsync($"/moviecollection/{_ratedCollectionId}");

        await Expect(_page.Locator("h1")).ToHaveTextAsync("Rated Movie");
        await Expect(_page.Locator(".collection-meta .agerating-badge")).ToHaveTextAsync("FSK 16");
    }

    [Fact]
    public async Task TvShowNfoRating_ShowsBadge_AndEpisodeInheritsIt()
    {
        if (_skipBrowser)
            return;

        await LoginAndGotoAsync($"/tvshow/{_showId}");

        await Expect(_page.Locator("h1")).ToHaveTextAsync("Rated Show");
        await Expect(_page.Locator(".tvshow-meta .agerating-badge")).ToHaveTextAsync("FSK 12");

        await _page.Locator(".episode-box").First.ClickAsync();
        await Expect(_page.Locator("h1")).ToHaveTextAsync("Pilot");
        await Expect(_page.Locator(".tvshow-meta .agerating-badge")).ToHaveTextAsync("FSK 12");
    }

    [Fact]
    public async Task Admin_SetsAgeRating_ViaEditDropdown()
    {
        if (_skipBrowser)
            return;

        await LoginAndGotoAsync($"/moviecollection/{_editCollectionId}");

        await Expect(_page.Locator("h1")).ToHaveTextAsync("Edit Movie");
        await Expect(_page.Locator(".agerating-badge")).ToHaveCountAsync(0);

        await _page.Locator("button[title='Bearbeiten']").ClickAsync();
        await _page.WaitForSelectorAsync(".metadata-agerating-select");
        await _page.SelectOptionAsync(".metadata-agerating-select", "FSK 12");
        await _page.Locator("button[title='Speichern']").ClickAsync();
        await _page.WaitForTimeoutAsync(2000);

        await Expect(_page.Locator(".collection-meta .agerating-badge")).ToHaveTextAsync("FSK 12");
    }

    private async Task LoginAndGotoAsync(string path)
    {
        await _page.GotoAsync($"{_serverUrl}/Account/Login?ReturnUrl={Uri.EscapeDataString(path)}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (_page.Url.Contains("/Account/Login"))
        {
            await _page.FillAsync("#email", "admin@test.com");
            await _page.FillAsync("#password", "P@ssw0rd123!");
            await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.Load);
        }

        await _page.GotoAsync($"{_serverUrl}{path}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(3000);
    }

    private async Task SeedAndScanAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await db.Database.EnsureCreatedAsync(ct);

            var admin = new ApplicationUser
            {
                UserName = "admin@test.com",
                Email = "admin@test.com",
                EmailConfirmed = true,
                IsAdmin = true
            };
            var createResult = await userManager.CreateAsync(admin, "P@ssw0rd123!");
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            await userManager.AddClaimAsync(admin, new Claim("IsAdmin", "True"));

            var source = new MediaSource
            {
                Name = "AgeRating Source",
                Path = _rootDir,
                SourceType = MediaSourceType.LocalDirectory,
                CreatedAt = DateTime.UtcNow
            };
            db.MediaSources.Add(source);
            await db.SaveChangesAsync(ct);
            db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = admin.Id });
            await db.SaveChangesAsync(ct);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
            var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();

            await scanner.ScanAllSourcesAsync(ct);
            while (await scanner.ScanNextMediaCollection(ct)) { }
            await classifier.ClassifyAllAsync(ct);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rated = await db.Movies.SingleAsync(m => m.Name == "Rated Movie", ct);
            Assert.Equal("FSK 16", rated.AgeRating);
            _ratedCollectionId = rated.MovieCollectionId!.Value;
            _editCollectionId = (await db.Movies.SingleAsync(m => m.Name == "Edit Movie", ct)).MovieCollectionId!.Value;
            _showId = (await db.TVShows.SingleAsync(s => s.Name == "Rated Show", ct)).Id;
        }
    }
}
