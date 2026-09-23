using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class DevicePairingE2ETests : IAsyncLifetime
{
    private const string AdminEmail = "pairing-admin@test.com";
    private const string AdminPassword = "P@ssw0rd123!";
    private const string UserEmail = "pairing-user@test.com";
    private const string UserPassword = "P@ssw0rd456!";
    private static readonly IPAddress LoopbackIp = IPAddress.Parse("127.0.0.1");

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private bool _skipBrowser;
    private string? _browserInfrastructureError;

    public DevicePairingE2ETests()
    {
        _dbPath = PairingWebApplicationFactory.CreateTempDbPath("vwp-device-pairing-e2e");
        _factory = PairingWebApplicationFactory.Create(_dbPath, builder =>
        {
            builder.UseUrls("http://127.0.0.1:0");
            builder.UseStaticWebAssets();
            builder.UseSetting("AutoUpdate:HostedServicesEnabled", "false");
        });
    }

    public async ValueTask InitializeAsync()
    {
        _factory.UseKestrel();
        _factory.StartServer();

        var server = _factory.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        _serverUrl = addressFeature!.Addresses.First().TrimEnd('/');

        await SeedAdminAsync();
        await SeedRegularUserAsync();

        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            _context = await _browser.NewContextAsync();
            _page = await _context.NewPageAsync();
            _page.SetDefaultTimeout(120_000);
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
        try { File.Delete(_dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp file may still be locked */ }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Admin_Creates_Pairing_Code_And_Sees_Code_With_Ttl()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail, AdminPassword);

        await _page.GotoAsync($"{_serverUrl}/admin/devices");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Kurz warten, bis die interaktive Blazor-Circuit-Verbindung steht,
        // sonst geht der Klick ins Leere.
        await _page.WaitForTimeoutAsync(1_000);
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Geraete", Exact = true })).ToBeVisibleAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Pairing-Code erzeugen" }).ClickAsync();

        var codeLocator = _page.Locator("[data-testid='generated-pairing-code']");
        await Expect(codeLocator).ToBeVisibleAsync();
        var code = await codeLocator.TextContentAsync();
        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.Equal(8, code!.Trim().Length);

        await Expect(_page.Locator("[data-testid='generated-pairing-code-expiry']")).ToContainTextAsync("Minuten");

        // Ersteller-Spalte zeigt die lesbare E-Mail-Adresse statt der Benutzer-Id (GUID)
        await Expect(_page.Locator("tr", new() { HasText = AdminEmail })).ToBeVisibleAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Admin_Renames_Paired_Device()
    {
        EnsureBrowserAvailable();
        UnblockLocalIp();
        await LoginAsync(AdminEmail, AdminPassword);

        var code = await CreatePairingCodeViaUiAsync();
        await ExchangeCodeForTokenAsync(code);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Aktualisieren" }).ClickAsync();
        var deviceRow = _page.Locator("tr", new() { HasText = "E2E-Geraet" });
        await Expect(deviceRow).ToBeVisibleAsync();

        await deviceRow.GetByRole(AriaRole.Button, new() { Name = "Umbenennen" }).ClickAsync();
        var input = _page.Locator("[data-testid='rename-device-input']");
        await Expect(input).ToBeVisibleAsync();
        await input.FillAsync("E2E-Wohnzimmer");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Expect(_page.Locator("tr", new() { HasText = "E2E-Wohnzimmer" })).ToBeVisibleAsync();
        await Expect(_page.Locator("tr", new() { HasText = "E2E-Geraet" })).ToHaveCountAsync(0);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Pairing_Flow_UiCode_Exchange_Login_Succeeds()
    {
        EnsureBrowserAvailable();
        UnblockLocalIp();
        await LoginAsync(AdminEmail, AdminPassword);

        var code = await CreatePairingCodeViaUiAsync();
        var deviceToken = await ExchangeCodeForTokenAsync(code);

        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_serverUrl}/api/auth/login");
        request.Headers.Add("X-API-Key", deviceToken);
        request.Content = JsonContent.Create(new AuthenticationRequest { Email = AdminEmail, Password = AdminPassword });
        var loginResponse = await http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content.ReadFromJsonAsync<AuthorizationToken>(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(token?.token));
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Revoked_Device_Token_Login_Fails()
    {
        EnsureBrowserAvailable();
        UnblockLocalIp();
        await LoginAsync(AdminEmail, AdminPassword);

        var code = await CreatePairingCodeViaUiAsync();
        var deviceToken = await ExchangeCodeForTokenAsync(code);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Aktualisieren" }).ClickAsync();
        var deviceRow = _page.Locator("tr", new() { HasText = "E2E-Geraet" });
        await Expect(deviceRow).ToBeVisibleAsync();
        await Expect(deviceRow.GetByRole(AriaRole.Cell, new() { Name = "Aktiv" })).ToBeVisibleAsync();

        await deviceRow.GetByRole(AriaRole.Button, new() { Name = "Widerrufen" }).ClickAsync();
        var confirmPanel = _page.Locator("[data-testid='revoke-confirmation']");
        await Expect(confirmPanel).ToBeVisibleAsync();
        await _page.Locator("#revoke-confirm").CheckAsync();
        await confirmPanel.GetByRole(AriaRole.Button, new() { Name = "Widerruf bestaetigen" }).ClickAsync();

        await Expect(confirmPanel).ToHaveCountAsync(0);
        await Expect(deviceRow.GetByRole(AriaRole.Cell, new() { Name = "Widerrufen" })).ToBeVisibleAsync();
        await Expect(deviceRow.GetByRole(AriaRole.Button, new() { Name = "Widerrufen" })).ToHaveCountAsync(0);

        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_serverUrl}/api/auth/login");
        request.Headers.Add("X-API-Key", deviceToken);
        request.Content = JsonContent.Create(new AuthenticationRequest { Email = AdminEmail, Password = AdminPassword });
        var loginResponse = await http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Revoke_Requires_Confirmation_And_Can_Be_Cancelled()
    {
        EnsureBrowserAvailable();
        UnblockLocalIp();
        await LoginAsync(AdminEmail, AdminPassword);

        var code = await CreatePairingCodeViaUiAsync();
        await ExchangeCodeForTokenAsync(code);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Aktualisieren" }).ClickAsync();
        var deviceRow = _page.Locator("tr", new() { HasText = "E2E-Geraet" });
        await Expect(deviceRow).ToBeVisibleAsync();
        await Expect(deviceRow.GetByRole(AriaRole.Cell, new() { Name = "Aktiv" })).ToBeVisibleAsync();

        await deviceRow.GetByRole(AriaRole.Button, new() { Name = "Widerrufen" }).ClickAsync();
        var confirmPanel = _page.Locator("[data-testid='revoke-confirmation']");
        await Expect(confirmPanel).ToBeVisibleAsync();
        var confirmButton = confirmPanel.GetByRole(AriaRole.Button, new() { Name = "Widerruf bestaetigen" });
        await Expect(confirmButton).ToBeDisabledAsync();

        // Ohne Haeckchen wird nichts widerrufen; Abbrechen schliesst den Dialog.
        await confirmPanel.GetByRole(AriaRole.Button, new() { Name = "Abbrechen" }).ClickAsync();
        await Expect(confirmPanel).ToHaveCountAsync(0);
        await Expect(deviceRow.GetByRole(AriaRole.Cell, new() { Name = "Aktiv" })).ToBeVisibleAsync();
        await Expect(deviceRow.GetByRole(AriaRole.Button, new() { Name = "Widerrufen" })).ToBeVisibleAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Invalid_Codes_Block_Ip_And_Show_In_Security()
    {
        EnsureBrowserAvailable();
        UnblockLocalIp();
        await LoginAsync(AdminEmail, AdminPassword);

        try
        {
            using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var clientKeyBase64 = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo());
            using var http = new HttpClient();

            for (var i = 0; i < 5; i++)
            {
                var failed = await http.PostAsJsonAsync(
                    $"{_serverUrl}/api/pairing/exchange",
                    new PairingExchangeRequest { Code = "ZZZZZZZZ", ClientPublicKey = clientKeyBase64 },
                    TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            }

            var blocked = await http.PostAsJsonAsync(
                $"{_serverUrl}/api/pairing/exchange",
                new PairingExchangeRequest { Code = "ZZZZZZZZ", ClientPublicKey = clientKeyBase64 },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

            await _page.GotoAsync($"{_serverUrl}/admin/security");
            await Expect(_page.Locator("tr", new() { HasText = "127.0.0.1" }).First).ToBeVisibleAsync();
        }
        finally
        {
            UnblockLocalIp();
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task NonAdmin_Sees_NotAuthorized_On_Devices_Page()
    {
        EnsureBrowserAvailable();
        await LoginAsync(UserEmail, UserPassword);

        await _page.GotoAsync($"{_serverUrl}/admin/devices");

        await Expect(_page.GetByText("Nicht autorisiert.")).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Pairing-Code erzeugen" })).ToHaveCountAsync(0);
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Gekoppelte Geraete" })).ToHaveCountAsync(0);
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Widerrufen" })).ToHaveCountAsync(0);
    }

    private void EnsureBrowserAvailable()
    {
        if (_skipBrowser)
            Assert.Fail($"Playwright-/Browser-Infrastruktur ist nicht verfuegbar: {_browserInfrastructureError}");
    }

    private void UnblockLocalIp()
        => _factory.Services.GetRequiredService<ILoginIpBlockService>().Unblock(LoopbackIp.ToString());

    private async Task LoginAsync(string email, string password)
    {
        await _page.GotoAsync($"{_serverUrl}/Account/Login");
        await _page.FillAsync("#email", email);
        await _page.FillAsync("#password", password);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);
    }

    private async Task<string> CreatePairingCodeViaUiAsync()
    {
        await _page.GotoAsync($"{_serverUrl}/admin/devices");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Kurz warten, bis die interaktive Blazor-Circuit-Verbindung steht,
        // sonst geht der Klick ins Leere.
        await _page.WaitForTimeoutAsync(1_000);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Pairing-Code erzeugen" }).ClickAsync();
        var codeLocator = _page.Locator("[data-testid='generated-pairing-code']");
        await Expect(codeLocator).ToBeVisibleAsync();
        var code = (await codeLocator.TextContentAsync())?.Trim();
        Assert.False(string.IsNullOrWhiteSpace(code));
        return code!;
    }

    private async Task<string> ExchangeCodeForTokenAsync(string code)
    {
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var http = new HttpClient();
        var exchangeResponse = await http.PostAsJsonAsync(
            $"{_serverUrl}/api/pairing/exchange",
            new PairingExchangeRequest
            {
                Code = code,
                ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
                DeviceName = "E2E-Geraet"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, exchangeResponse.StatusCode);
        var payload = await exchangeResponse.Content.ReadFromJsonAsync<PairingExchangeResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
        return PairingCryptoHelper.DecryptToken(clientKey, payload!.ServerPublicKey, payload.EncryptedToken);
    }

    private async Task SeedAdminAsync()
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
        var createResult = await userManager.CreateAsync(admin, AdminPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException($"Admin-Testbenutzer konnte nicht erstellt werden: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

        await userManager.AddClaimAsync(admin, new Claim("IsAdmin", "True"));
    }

    private async Task SeedRegularUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = UserEmail,
            Email = UserEmail,
            EmailConfirmed = true,
            IsAdmin = false
        };
        var createResult = await userManager.CreateAsync(user, UserPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException($"Testbenutzer konnte nicht erstellt werden: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
    }
}
