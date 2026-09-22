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
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end-Tests mit Playwright: Ein Administrator legt eine Medienquelle vom Typ
/// "Lokales Verzeichnis" über die Oberfläche an, sieht eine Fehlermeldung bei
/// ungültigen Pfaden und kann den Quelltyp bei bestehenden Quellen nicht ändern.
/// </summary>
[Trait("Category", "E2E")]
public sealed class MediaSourceLocalDirectoryE2ETests : IAsyncLifetime
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

    public MediaSourceLocalDirectoryE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-local-e2e-{Guid.NewGuid()}.db");
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
    public async Task Admin_Can_Create_LocalDirectory_Source_And_SftpFields_Are_Hidden()
    {
        if (_skipBrowser)
            return;

        var localDir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-local-{Guid.NewGuid():N}");
        Directory.CreateDirectory(localDir);
        try
        {
            await LoginAsAdminAsync();

            await _page.GetByRole(AriaRole.Button, new() { Name = "Neu" }).ClickAsync();
            await _page.Locator("#source-type").WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await _page.WaitForTimeoutAsync(1000);

            // SFTP-Felder sind zunächst sichtbar (Default: SFTP-Server).
            Assert.True(await _page.Locator("#source-host").CountAsync() > 0);

            await _page.SelectOptionAsync("#source-type", new SelectOptionValue { Label = "Lokales Verzeichnis" });
            await _page.WaitForTimeoutAsync(500);

            // Bei lokalem Typ werden die SFTP-Felder nicht mehr gerendert.
            Assert.Equal(0, await _page.Locator("#source-host").CountAsync());
            Assert.Equal(0, await _page.Locator("#source-username").CountAsync());

            await _page.FillAsync("#source-name", "Lokale Testquelle");
            await _page.FillAsync("#source-path", localDir);
            await _page.GetByRole(AriaRole.Button, new() { Name = "Anlegen" }).ClickAsync();

            var row = _page.Locator("table tbody tr", new() { HasText = "Lokale Testquelle" });
            await row.WaitForAsync(new() { State = WaitForSelectorState.Visible });

            Assert.Contains("Lokal", await row.TextContentAsync());
        }
        finally
        {
            try { Directory.Delete(localDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Admin_SeesError_When_Local_Directory_Does_Not_Exist()
    {
        if (_skipBrowser)
            return;

        var missingDir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-missing-{Guid.NewGuid():N}");

        await LoginAsAdminAsync();

        await _page.GetByRole(AriaRole.Button, new() { Name = "Neu" }).ClickAsync();
        await _page.Locator("#source-type").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.WaitForTimeoutAsync(1000);

        await _page.SelectOptionAsync("#source-type", new SelectOptionValue { Label = "Lokales Verzeichnis" });
        await _page.WaitForTimeoutAsync(500);

        await _page.FillAsync("#source-name", "Ungueltige Quelle");
        await _page.FillAsync("#source-path", missingDir);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Anlegen" }).ClickAsync();

        var alert = _page.Locator(".alert-danger", new() { HasText = "Verzeichnis existiert nicht" });
        await alert.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        Assert.Contains("/admin/mediasources/new", _page.Url);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.MediaSources.AnyAsync(s => s.Name == "Ungueltige Quelle"));
    }

    [Fact]
    public async Task Admin_SeesError_When_Local_Directory_Is_Not_Accessible()
    {
        if (_skipBrowser)
            return;

        // Existierendes Verzeichnis ohne Lesezugriff: muss als "kann nicht zugegriffen"
        // gemeldet werden, nicht fälschlich als "Verzeichnis existiert nicht".
        var deniedDir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-denied-{Guid.NewGuid():N}");
        Directory.CreateDirectory(deniedDir);
        try
        {
            TestHelpers.DenyReadAccess(deniedDir);

            await LoginAsAdminAsync();

            await _page.GetByRole(AriaRole.Button, new() { Name = "Neu" }).ClickAsync();
            await _page.Locator("#source-type").WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await _page.WaitForTimeoutAsync(1000);

            await _page.SelectOptionAsync("#source-type", new SelectOptionValue { Label = "Lokales Verzeichnis" });
            await _page.WaitForTimeoutAsync(500);

            await _page.FillAsync("#source-name", "Gesperrte Quelle");
            await _page.FillAsync("#source-path", deniedDir);
            await _page.GetByRole(AriaRole.Button, new() { Name = "Anlegen" }).ClickAsync();

            var alert = _page.Locator(".alert-danger", new() { HasText = "kann nicht zugegriffen" });
            await alert.WaitForAsync(new() { State = WaitForSelectorState.Visible });

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.MediaSources.AnyAsync(s => s.Name == "Gesperrte Quelle"));
        }
        finally
        {
            TestHelpers.ResetAccessControl(deniedDir);
            try { Directory.Delete(deniedDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Edit_LocalSource_TypeSelect_Is_Disabled()
    {
        if (_skipBrowser)
            return;

        var localDir = Path.Combine(Path.GetTempPath(), $"vwp-e2e-edit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(localDir);
        try
        {
            long sourceId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var source = new MediaSource
                {
                    Name = "Bestehende lokale Quelle",
                    Path = localDir,
                    SourceType = MediaSourceType.LocalDirectory,
                    Host = string.Empty,
                    Port = 0,
                    CreatedAt = DateTime.UtcNow
                };
                db.MediaSources.Add(source);
                await db.SaveChangesAsync();
                sourceId = source.Id;
            }

            await LoginAsAdminAsync();

            await _page.GotoAsync($"{_serverUrl}/admin/mediasources/{sourceId}");
            await _page.Locator("#source-type").WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await _page.WaitForTimeoutAsync(1000);

            Assert.True(await _page.Locator("#source-type").IsDisabledAsync());
            Assert.Equal("LocalDirectory", await _page.Locator("#source-type").InputValueAsync());
            Assert.Equal(0, await _page.Locator("#source-host").CountAsync());

            // Speichern bleibt möglich und führt zurück zur Übersicht.
            await _page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
            var row = _page.Locator("table tbody tr", new() { HasText = "Bestehende lokale Quelle" });
            await row.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        }
        finally
        {
            try { Directory.Delete(localDir, recursive: true); } catch { /* ignore */ }
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

        Assert.True(
            _page.Url.Contains("/admin/mediasources"),
            $"After login URL: {_page.Url}, console: {string.Join(" | ", _consoleMessages.TakeLast(10))}");

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
        {
            throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        await userManager.AddClaimAsync(admin, new Claim("IsAdmin", "True"));
    }
}
