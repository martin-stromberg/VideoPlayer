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
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end-Test mit Playwright: Der Klick auf "Neu erfassen" in <c>MediaSourceExplorer.razor</c>
/// scannt nur den Teilbaum der aktuellen Collection (Akzeptanzszenario 2) und weist auf eine bereits
/// laufende Klassifizierung hin (Akzeptanzszenario 4).
/// </summary>
[Trait("Category", "E2E")]
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class MediaSourceExplorerRescanE2ETests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly string _rootDir;
    private readonly string _dirA;
    private readonly string _dirB;
    private readonly GatedMetadataWriteCoordinator _gatedWriteCoordinator = new();
    private readonly WebApplicationFactory<global::Program> _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private bool _skipBrowser;
    private long _sourceId;
    private long _collectionAId;
    private long _collectionBId;

    public MediaSourceExplorerRescanE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-explorer-rescan-e2e-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-explorer-rescan-e2e-{Guid.NewGuid():N}");
        _dirA = Path.Combine(_rootDir, "SeriesA");
        _dirB = Path.Combine(_rootDir, "SeriesB");
        Directory.CreateDirectory(_dirA);
        Directory.CreateDirectory(_dirB);
        File.WriteAllText(Path.Combine(_dirA, "a1.mkv"), "video");
        File.WriteAllText(Path.Combine(_dirB, "b1.mkv"), "video");

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
                    // Ersetzt den produktiven Schreibsperren-Koordinator, damit ein Test gezielt eine
                    // laufende Klassifizierung simulieren kann (Akzeptanzszenario 4).
                    services.AddSingleton<IMediaMetadataWriteCoordinator>(_gatedWriteCoordinator);
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
        _gatedWriteCoordinator.Release();

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
    public async Task RescanCurrent_FindsNewFile_InSubtreeOnly_SiblingUnchanged()
    {
        if (_skipBrowser)
            return;

        File.WriteAllText(Path.Combine(_dirA, "a2.mkv"), "video");

        await LoginAsAdminAsync();
        await _page.GotoAsync($"{_serverUrl}/admin/mediasources/{_sourceId}/explorer?collectionId={_collectionAId}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(2000);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Neu erfassen" }).ClickAsync();

        var newFileEntry = _page.Locator(".list-group-item", new() { HasText = "a2.mkv" });
        await newFileEntry.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });

        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.MediaItems.CountAsync(i => i.MediaCollectionId == _collectionAId, ct));
        Assert.Equal(1, await db.MediaItems.CountAsync(i => i.MediaCollectionId == _collectionBId, ct));
    }

    [Fact]
    public async Task RescanCurrent_WhileClassificationRunning_ShowsDeferredHint()
    {
        if (_skipBrowser)
            return;

        _gatedWriteCoordinator.Hold();

        // Belegt die statische Klassifizierungssperre, während die Schreibsperre zurückgehalten wird.
        using var occupyingScope = _factory.Services.CreateScope();
        var occupyingClassifier = occupyingScope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();
        var occupyingTask = occupyingClassifier.ClassifyCollectionTreeAsync(_collectionAId, TestContext.Current.CancellationToken);

        try
        {
            await _gatedWriteCoordinator.WaitUntilEnteredAsync(TestContext.Current.CancellationToken);

            await LoginAsAdminAsync();
            await _page.GotoAsync($"{_serverUrl}/admin/mediasources/{_sourceId}/explorer?collectionId={_collectionBId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await _page.WaitForTimeoutAsync(2000);

            await _page.GetByRole(AriaRole.Button, new() { Name = "Neu erfassen" }).ClickAsync();

            var hint = _page.Locator(".alert-info", new() { HasText = "Klassifizierung läuft im Hintergrund" });
            await hint.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        }
        finally
        {
            _gatedWriteCoordinator.Release();
            await occupyingTask;
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
            Name = "Explorer Rescan Source",
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
        _sourceId = source.Id;

        // Einmaliger Komplettscan außerhalb der UI, damit der Explorer bereits eine Collection-Struktur
        // vorfindet, in die der Test navigieren kann.
        var coordinator = scope.ServiceProvider.GetRequiredService<MediaSourceScanCoordinator>();
        await coordinator.RunFullScanAsync("Automatischer Scanprozess", null, TestContext.Current.CancellationToken);

        _collectionAId = (await db.MediaCollections.SingleAsync(c => c.Path == _dirA)).Id;
        _collectionBId = (await db.MediaCollections.SingleAsync(c => c.Path == _dirB)).Id;
    }
}
