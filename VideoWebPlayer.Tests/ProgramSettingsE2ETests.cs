using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramSettingsE2ETests : IAsyncLifetime
{
    private const string AdminEmail = "program-settings-admin@test.com";
    private const string UserEmail = "program-settings-user@test.com";
    private const string TestPassword = "P@ssw0rd123!";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private readonly List<string> _consoleMessages = [];
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private bool _skipBrowser;
    private string? _browserInfrastructureError;

    public ProgramSettingsE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-program-settings-e2e-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        _factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseUrls("http://127.0.0.1:0");
                builder.UseStaticWebAssets();
                builder.UseSetting("AutoUpdate:HostedServicesEnabled", "false");
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

        await SeedUserAsync(AdminEmail, isAdmin: true);
        await SeedUserAsync(UserEmail, isAdmin: false);

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
    [Trait("Category", "E2E")]
    public async Task Admin_TogglesMdnsAdvertisement_AndSettingPersists()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail);
        await _page.GotoAsync($"{_serverUrl}/admin/program-settings");
        await WaitForInteractivePageAsync();

        var toggle = _page.GetByLabel("Server per mDNS im Netzwerk ankündigen");
        await Expect(toggle).ToBeCheckedAsync();

        await toggle.UncheckAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(_page.GetByText("Gespeichert.")).ToBeVisibleAsync();
        Assert.False(await GetMdnsAdvertisementEnabledAsync());

        await _page.GotoAsync($"{_serverUrl}/admin/program-settings");
        await WaitForInteractivePageAsync();
        toggle = _page.GetByLabel("Server per mDNS im Netzwerk ankündigen");
        await Expect(toggle).Not.ToBeCheckedAsync();

        await toggle.CheckAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(_page.GetByText("Gespeichert.")).ToBeVisibleAsync();
        Assert.True(await GetMdnsAdvertisementEnabledAsync());

        await _page.GotoAsync($"{_serverUrl}/admin/program-settings");
        await WaitForInteractivePageAsync();
        await Expect(_page.GetByLabel("Server per mDNS im Netzwerk ankündigen")).ToBeCheckedAsync();

        Assert.Empty(SevereErrors());
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Admin_SavesDiscoveryPublicBaseUrl_AndSettingPersists()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail);
        await _page.GotoAsync($"{_serverUrl}/admin/program-settings");
        await WaitForInteractivePageAsync();

        var field = _page.Locator("#discoveryPublicBaseUrl");
        await field.FillAsync("https://videos.example.com/videoplayer/");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(_page.GetByText("Gespeichert.")).ToBeVisibleAsync();
        Assert.Equal("https://videos.example.com/videoplayer/", await GetDiscoveryPublicBaseUrlAsync());

        await _page.GotoAsync($"{_serverUrl}/admin/program-settings");
        await WaitForInteractivePageAsync();
        await Expect(_page.Locator("#discoveryPublicBaseUrl")).ToHaveValueAsync("https://videos.example.com/videoplayer/");

        // Löschpfad: Feld leeren und speichern setzt den Admin-Override zurück.
        await _page.Locator("#discoveryPublicBaseUrl").FillAsync("");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Expect(_page.GetByText("Gespeichert.")).ToBeVisibleAsync();
        Assert.Null(await GetDiscoveryPublicBaseUrlAsync());

        Assert.Empty(SevereErrors());
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Admin_EntersInvalidDiscoveryPublicBaseUrl_ValidationBlocksSave()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail);
        await _page.GotoAsync($"{_serverUrl}/admin/program-settings");
        await WaitForInteractivePageAsync();

        await _page.Locator("#discoveryPublicBaseUrl").FillAsync("keine-url");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Expect(_page.Locator(".validation-message").First).ToBeVisibleAsync();
        await Expect(_page.Locator(".validation-message").First).ToContainTextAsync("absolute http- oder https-URL");
        await Expect(_page.GetByText("Gespeichert.")).ToHaveCountAsync(0);
        Assert.Null(await GetDiscoveryPublicBaseUrlAsync());

        Assert.Empty(SevereErrors());
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task NonAdmin_GetsNotAuthorized_OnProgramSettings()
    {
        EnsureBrowserAvailable();
        await LoginAsync(UserEmail);
        await _page.GotoAsync($"{_serverUrl}/admin/program-settings");

        await Expect(_page.GetByText("Nicht autorisiert.")).ToBeVisibleAsync();
        await Expect(_page.Locator("#mdnsAdvertisementEnabled")).ToHaveCountAsync(0);
        await Expect(_page.Locator("#discoveryPublicBaseUrl")).ToHaveCountAsync(0);

        Assert.Empty(SevereErrors());
    }

    private async Task<string?> GetDiscoveryPublicBaseUrlAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ProgramSettingsService>();
        return await settings.GetDiscoveryPublicBaseUrlAsync();
    }

    private async Task<bool> GetMdnsAdvertisementEnabledAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ProgramSettingsService>();
        return await settings.GetMdnsAdvertisementEnabledAsync();
    }

    private IReadOnlyList<string> SevereErrors()
        => _consoleMessages
            .Where(m => m.StartsWith("[page-error]") ||
                        (m.StartsWith("[response]") && (m.Contains(" 4") || m.Contains(" 5"))))
            .ToList();

    private void EnsureBrowserAvailable()
    {
        if (_skipBrowser)
            Assert.Fail($"Playwright-/Browser-Infrastruktur ist nicht verfügbar: {_browserInfrastructureError}");
    }

    private async Task LoginAsync(string email)
    {
        await _page.GotoAsync($"{_serverUrl}/Account/Login");
        await _page.FillAsync("#email", email);
        await _page.FillAsync("#password", TestPassword);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);
    }

    private async Task WaitForInteractivePageAsync()
    {
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Allgemein" })).ToBeVisibleAsync();
        await _page.WaitForTimeoutAsync(1_000);
    }

    private async Task SeedUserAsync(string email, bool isAdmin)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsAdmin = isAdmin
        };
        var createResult = await userManager.CreateAsync(user, TestPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException($"Testbenutzer konnte nicht erstellt werden: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

        if (isAdmin)
            await userManager.AddClaimAsync(user, new Claim("IsAdmin", "True"));
    }
}
