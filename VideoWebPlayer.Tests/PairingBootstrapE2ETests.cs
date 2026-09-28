using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
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

/// <summary>
/// Browser tests for the profile-page device pairing flow (QR bootstrap ticket).
/// </summary>
public sealed class PairingBootstrapE2ETests : IAsyncLifetime
{
    private const string UserEmail = "bootstrap-user@test.com";
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

    public PairingBootstrapE2ETests()
    {
        _dbPath = PairingWebApplicationFactory.CreateTempDbPath("vwp-bootstrap-e2e");
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
        await Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Profile_DevicesPage_CreateTicket_Bootstrap_Succeeds()
    {
        EnsureBrowserAvailable();
        UnblockLocalIp();
        await LoginAsync(UserEmail, UserPassword);

        await _page.GotoAsync($"{_serverUrl}/Account/Manage/Devices");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Gerät koppeln" })).ToBeVisibleAsync();

        await _page.GetByRole(AriaRole.Button, new() { Name = "Gerät koppeln" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // QR-Bild, Kurzcode, Gueltigkeit und localhost-Hinweis werden angezeigt.
        var qrImage = _page.Locator("[data-testid='bootstrap-qr-image']");
        await Expect(qrImage).ToBeVisibleAsync();
        var qrSrc = await qrImage.GetAttributeAsync("src");
        Assert.StartsWith("data:image/png;base64,", qrSrc);

        var shortCodeLocator = _page.Locator("[data-testid='bootstrap-short-code']");
        await Expect(shortCodeLocator).ToBeVisibleAsync();
        var shortCode = (await shortCodeLocator.TextContentAsync())?.Trim();
        Assert.False(string.IsNullOrWhiteSpace(shortCode));
        Assert.Equal(8, shortCode!.Length);

        await Expect(_page.Locator("[data-testid='bootstrap-ticket-expiry']")).ToContainTextAsync("Gültig bis");
        await Expect(_page.Locator("[data-testid='bootstrap-localhost-warning']")).ToBeVisibleAsync();

        // Screenshot als Review-Beleg ablegen.
        var screenshotDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestResults");
        Directory.CreateDirectory(screenshotDir);
        await _page.ScreenshotAsync(new()
        {
            FullPage = true,
            Path = Path.Combine(screenshotDir, "pairing-bootstrap-devices-page.png")
        });

        // Der Kurzcode loest das Ticket am oeffentlichen Bootstrap-Endpunkt ein.
        using var clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var http = new HttpClient();
        var bootstrapResponse = await http.PostAsJsonAsync(
            $"{_serverUrl}/api/pairing/bootstrap",
            new PairingBootstrapRequest
            {
                Ticket = shortCode,
                ClientPublicKey = Convert.ToBase64String(clientKey.ExportSubjectPublicKeyInfo()),
                DeviceName = "E2E-Bootstrap-Geraet"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, bootstrapResponse.StatusCode);

        var body = await bootstrapResponse.Content.ReadFromJsonAsync<PairingBootstrapResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        var json = PairingCryptoHelper.DecryptToken(clientKey, body!.ServerPublicKey, body.EncryptedPayload);
        var payload = System.Text.Json.JsonSerializer.Deserialize<PairingBootstrapPayload>(
            json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.DeviceToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(payload.Token);
        Assert.NotNull(jwt.Subject);

        // Refresh-Token rotiert die Session (Device-Token als Gate-Key).
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, $"{_serverUrl}/api/auth/refresh");
        refreshRequest.Headers.Add("X-API-Key", payload.DeviceToken);
        refreshRequest.Content = JsonContent.Create(new RefreshTokenRequest { RefreshToken = payload.RefreshToken });
        var refreshResponse = await http.SendAsync(refreshRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task DevicesPage_RequiresAuthentication()
    {
        EnsureBrowserAvailable();

        await _page.GotoAsync($"{_serverUrl}/Account/Manage/Devices");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.Contains("/Account/Login", _page.Url);
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Gerät koppeln" })).ToHaveCountAsync(0);
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

    private async Task SeedRegularUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

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
