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
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end-Test mit Playwright: Der Klick auf "Komplettscan aller Quellen" in
/// <c>MediaSourceAdmin.razor</c> findet eine neue Serien-Datei, zeigt die Ergebnismeldung und blockiert
/// korrekt, solange ein Restore läuft (Akzeptanzszenarien 1 und 3).
/// </summary>
[Trait("Category", "E2E")]
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class MediaSourceAdminFullScanE2ETests : IAsyncLifetime
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

    public MediaSourceAdminFullScanE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-admin-fullscan-e2e-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-admin-fullscan-e2e-{Guid.NewGuid():N}");
        var showDir = Path.Combine(_rootDir, "AdminScanShow");
        Directory.CreateDirectory(showDir);
        File.WriteAllText(Path.Combine(showDir, "tvshow.nfo"), "<tvshow><title>Admin Scan Show</title></tvshow>");
        File.WriteAllText(Path.Combine(showDir, "episode01.mkv"), "video");
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

        await SeedDatabaseAsync();

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
    public async Task Admin_RunsFullScan_AndNewEpisodeAppearsInExplorer()
    {
        if (_skipBrowser)
            return;

        await LoginAsAdminAsync();

        await _page.GetByRole(AriaRole.Button, new() { Name = "Komplettscan aller Quellen" }).ClickAsync();

        var status = _page.Locator(".alert-info", new() { HasText = "Komplettscan abgeschlossen" });
        await status.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });

        long sourceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ct = TestContext.Current.CancellationToken;
            sourceId = (await db.MediaSources.SingleAsync(ct)).Id;
            Assert.True(await db.TVShows.AnyAsync(s => s.Name == "Admin Scan Show", ct));
        }

        await _page.GotoAsync($"{_serverUrl}/admin/mediasources/{sourceId}/explorer");
        var showEntry = _page.Locator(".list-group-item", new() { HasText = "AdminScanShow" });
        await showEntry.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await showEntry.ClickAsync();

        var episodeEntry = _page.Locator(".list-group-item", new() { HasText = "episode01.mkv" });
        await episodeEntry.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    [Fact]
    public async Task Admin_FullScanBlocked_WhileRestoreIsActive()
    {
        if (_skipBrowser)
            return;

        var gate = _factory.Services.GetRequiredService<IBackgroundProcessingGate>();
        var restoreLease = await gate.PauseForRestoreAsync(TestContext.Current.CancellationToken);

        try
        {
            await LoginAsAdminAsync();

            await _page.GetByRole(AriaRole.Button, new() { Name = "Komplettscan aller Quellen" }).ClickAsync();

            var status = _page.Locator(".alert-info", new() { HasText = "Restore läuft" });
            await status.WaitForAsync(new() { State = WaitForSelectorState.Visible });

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.MediaCollections.AnyAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await restoreLease.DisposeAsync();
        }
    }

    private async Task LoginAsAdminAsync()
    {
        await _page.GotoAsync($"{_serverUrl}/Account/Login?ReturnUrl=admin%2Fmediasources");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await _page.FillAsync("#email", "admin@test.com");
        await _page.FillAsync("#password", "P@ssw0rd123!");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);

        Assert.True(_page.Url.Contains("/admin/mediasources"), $"After login URL: {_page.Url}");

        // Give Blazor Server enough time to switch from prerender to interactive.
        await _page.WaitForTimeoutAsync(3000);
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
            throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        await userManager.AddClaimAsync(admin, new Claim("IsAdmin", "True"));

        var source = new MediaSource
        {
            Name = "Admin Scan Source",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory,
            Host = string.Empty,
            Port = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();
        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = admin.Id });
        await db.SaveChangesAsync();
    }
}
