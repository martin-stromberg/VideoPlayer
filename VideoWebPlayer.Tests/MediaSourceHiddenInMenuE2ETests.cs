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
/// End-to-end-Tests mit Playwright zum Merkmal „Im Menü ausblenden" (Issue #284): Ein Administrator
/// setzt die Checkbox auf der Quellen-Detailseite, danach verschwindet der Menüeintrag für Admins
/// und Nicht-Admins — der Direktaufruf der Quelle bleibt dagegen erreichbar.
/// </summary>
[Trait("Category", "E2E")]
public sealed class MediaSourceHiddenInMenuE2ETests : IAsyncLifetime
{
    private const string AdminEmail = "admin@test.com";
    private const string UserEmail = "user@test.com";
    private const string Password = "P@ssw0rd123!";
    private const string VisibleSourceName = "Sichtbare Quelle";
    private const string HiddenSourceName = "Clip-Quelle";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private readonly List<string> _consoleMessages = [];
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private long _visibleSourceId;
    private long _hiddenSourceId;
    private bool _skipBrowser;
    private string? _browserInfrastructureError;

    public MediaSourceHiddenInMenuE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-hiddeninmenu-e2e-{Guid.NewGuid()}.db");
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
        catch (PlaywrightException ex)
        {
            _skipBrowser = true;
            _browserInfrastructureError = ex.Message;
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
    public async Task Admin_HidesSource_ViaCheckbox_FlagPersists_AndMenuEntryDisappears()
    {
        FailWhenBrowserMissing();
        await SetHiddenInMenuAsync(_hiddenSourceId, false);

        await LoginAsync(AdminEmail, "/admin/mediasources");
        await AssertMenuLinkCountAsync(_hiddenSourceId, 1);

        await _page.GotoAsync($"{_serverUrl}/admin/mediasources/{_hiddenSourceId}");
        await _page.Locator("#source-hidden-in-menu").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.WaitForTimeoutAsync(1000);

        await _page.CheckAsync("#source-hidden-in-menu");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        // Zurück in der Übersicht — Flag muss persistiert sein.
        await _page.Locator("table tbody tr", new() { HasText = HiddenSourceName })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.True(await ReadHiddenInMenuAsync(_hiddenSourceId));

        // Nach Reload der Detailseite ist die Checkbox weiterhin gesetzt.
        await _page.GotoAsync($"{_serverUrl}/admin/mediasources/{_hiddenSourceId}");
        await _page.Locator("#source-hidden-in-menu").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.WaitForTimeoutAsync(1000);
        await Expect(_page.Locator("#source-hidden-in-menu")).ToBeCheckedAsync();

        // Der Menüeintrag der versteckten Quelle verschwindet auch für den Admin.
        await _page.GotoAsync($"{_serverUrl}/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await AssertMenuLinkCountAsync(_hiddenSourceId, 0);
        await AssertMenuLinkCountAsync(_visibleSourceId, 1);
    }

    [Fact]
    public async Task Regular_User_Sees_Only_Visible_Source_In_Menu()
    {
        FailWhenBrowserMissing();
        await SetHiddenInMenuAsync(_hiddenSourceId, true);

        await LoginAsync(UserEmail, "/");

        await AssertMenuLinkCountAsync(_visibleSourceId, 1);
        await AssertMenuLinkCountAsync(_hiddenSourceId, 0);
        // Die Sektions-Überschrift bleibt, solange eine sichtbare Quelle existiert.
        await Expect(_page.Locator("nav .nav-link", new() { HasText = "Quellen" })).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Direct_Access_To_Hidden_Source_Remains_Reachable()
    {
        FailWhenBrowserMissing();
        await SetHiddenInMenuAsync(_hiddenSourceId, true);

        await LoginAsync(UserEmail, "/");

        await _page.GotoAsync($"{_serverUrl}/mediasource/{_hiddenSourceId}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(_page.Locator("h1")).ToHaveTextAsync(HiddenSourceName);
    }

    [Fact]
    public async Task Menu_Hides_Sources_Section_When_All_Shared_Sources_Hidden()
    {
        FailWhenBrowserMissing();
        await SetHiddenInMenuAsync(_hiddenSourceId, true);
        await SetHiddenInMenuAsync(_visibleSourceId, true);

        await LoginAsync(UserEmail, "/");

        await Expect(_page.Locator("nav a.nav-link[href^='/mediasource/']")).ToHaveCountAsync(0);
        await Expect(_page.Locator("nav .nav-link", new() { HasText = "Quellen" })).ToHaveCountAsync(0);
    }

    private void FailWhenBrowserMissing()
    {
        if (_skipBrowser)
            Assert.Fail($"Playwright-/Browser-Infrastruktur ist nicht verfügbar: {_browserInfrastructureError}\n{string.Join("\n", _consoleMessages)}");
    }

    private async Task AssertMenuLinkCountAsync(long sourceId, int expected)
    {
        var link = _page.Locator($"nav a.nav-link[href='/mediasource/{sourceId}']");
        await Expect(link).ToHaveCountAsync(expected);
    }

    private async Task SetHiddenInMenuAsync(long sourceId, bool value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var source = await db.MediaSources.FindAsync([sourceId], TestContext.Current.CancellationToken);
        Assert.NotNull(source);
        if (source!.HiddenInMenu == value)
            return;

        source.HiddenInMenu = value;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<bool> ReadHiddenInMenuAsync(long sourceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var source = await db.MediaSources.AsNoTracking().FirstAsync(s => s.Id == sourceId, TestContext.Current.CancellationToken);
        return source.HiddenInMenu;
    }

    private async Task LoginAsync(string email, string returnPath)
    {
        var returnUrl = Uri.EscapeDataString(returnPath.TrimStart('/'));
        await _page.GotoAsync($"{_serverUrl}/Account/Login?ReturnUrl={returnUrl}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await _page.FillAsync("#email", email);
        await _page.FillAsync("#password", Password);
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
            UserName = AdminEmail,
            Email = AdminEmail,
            EmailConfirmed = true,
            IsAdmin = true
        };
        var adminResult = await userManager.CreateAsync(admin, Password);
        if (!adminResult.Succeeded)
            throw new InvalidOperationException($"Testadmin konnte nicht erstellt werden: {string.Join(", ", adminResult.Errors.Select(e => e.Description))}");
        await userManager.AddClaimAsync(admin, new Claim("IsAdmin", "True"));

        var user = new ApplicationUser
        {
            UserName = UserEmail,
            Email = UserEmail,
            EmailConfirmed = true
        };
        var userResult = await userManager.CreateAsync(user, Password);
        if (!userResult.Succeeded)
            throw new InvalidOperationException($"Testbenutzer konnte nicht erstellt werden: {string.Join(", ", userResult.Errors.Select(e => e.Description))}");

        var visibleSource = CreateSource(VisibleSourceName, "/visible");
        var hiddenSource = CreateSource(HiddenSourceName, "/hidden");
        db.MediaSources.AddRange(visibleSource, hiddenSource);
        await db.SaveChangesAsync();

        _visibleSourceId = visibleSource.Id;
        _hiddenSourceId = hiddenSource.Id;

        db.MediaSourceUsers.AddRange(
            new MediaSourceUser { MediaSourceId = visibleSource.Id, UserId = admin.Id },
            new MediaSourceUser { MediaSourceId = hiddenSource.Id, UserId = admin.Id },
            new MediaSourceUser { MediaSourceId = visibleSource.Id, UserId = user.Id },
            new MediaSourceUser { MediaSourceId = hiddenSource.Id, UserId = user.Id });
        await db.SaveChangesAsync();
    }

    private static MediaSource CreateSource(string name, string path)
        => new()
        {
            Name = name,
            Host = "127.0.0.1",
            Port = 22,
            Path = path,
            Username = "user",
            Password = "pass"
        };
}
