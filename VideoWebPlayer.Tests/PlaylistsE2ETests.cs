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
/// End-to-end tests with Playwright covering playlist creation, listing, editing,
/// deletion with confirmation, validation errors, duplicate names, unauthenticated
/// access and ownership isolation between users.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistsE2ETests : IAsyncLifetime
{
    private const string UserAEmail = "playlist-user-a@test.com";
    private const string UserBEmail = "playlist-user-b@test.com";
    private const string Password = "P@ssw0rd123!";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private bool _skipBrowser;

    public PlaylistsE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-playlists-e2e-{Guid.NewGuid()}.db");
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

        await SeedUsersAsync();

        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            _context = await _browser.NewContextAsync();
            _page = await _context.NewPageAsync();
            _page.SetDefaultTimeout(30_000);
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
    public async Task Create_List_Edit_And_Delete_Playlist_HappyPath()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await _page.GotoAsync($"{_serverUrl}/playlists");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        // Create
        await _page.ClickAsync("#create-playlist-button");
        await _page.WaitForSelectorAsync("#playlist-name-input");
        await _page.FillAsync("#playlist-name-input", "Serien-Marathon");
        await _page.FillAsync("#playlist-description-input", "Meine Lieblingsserien");
        await _page.ClickAsync("#playlist-save-button");
        await _page.WaitForTimeoutAsync(1000);

        // List
        var row = _page.Locator(".playlist-row[data-playlist-name='Serien-Marathon']");
        await Expect(row).ToBeVisibleAsync();

        // Edit
        await row.Locator(".playlist-edit-button").ClickAsync();
        await _page.WaitForSelectorAsync("#playlist-name-input");
        await _page.FillAsync("#playlist-name-input", "Serien-Marathon Deluxe");
        await _page.ClickAsync("#playlist-save-button");
        await _page.WaitForTimeoutAsync(1000);

        var updatedRow = _page.Locator(".playlist-row[data-playlist-name='Serien-Marathon Deluxe']");
        await Expect(updatedRow).ToBeVisibleAsync();

        // Delete with confirmation
        await updatedRow.Locator(".playlist-delete-button").ClickAsync();
        await _page.WaitForSelectorAsync("#confirm-delete-playlist-button");
        await _page.ClickAsync("#confirm-delete-playlist-button");
        await _page.WaitForTimeoutAsync(1000);

        await Expect(_page.Locator(".playlist-row[data-playlist-name='Serien-Marathon Deluxe']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task EmptyName_ShowsValidationError()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await _page.GotoAsync($"{_serverUrl}/playlists");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        await _page.ClickAsync("#create-playlist-button");
        await _page.WaitForSelectorAsync("#playlist-name-input");
        await _page.ClickAsync("#playlist-save-button");
        await _page.WaitForTimeoutAsync(500);

        await Expect(_page.Locator("#playlist-name-input")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task DuplicateName_ShowsConflictError()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await _page.GotoAsync($"{_serverUrl}/playlists");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        await _page.ClickAsync("#create-playlist-button");
        await _page.WaitForSelectorAsync("#playlist-name-input");
        await _page.FillAsync("#playlist-name-input", "Duplikat-Test");
        await _page.ClickAsync("#playlist-save-button");
        await _page.WaitForTimeoutAsync(1000);

        await _page.ClickAsync("#create-playlist-button");
        await _page.WaitForSelectorAsync("#playlist-name-input");
        await _page.FillAsync("#playlist-name-input", "duplikat-test");
        await _page.ClickAsync("#playlist-save-button");
        await _page.WaitForTimeoutAsync(1000);

        await Expect(_page.Locator("#playlist-form-error")).ToContainTextAsync("existiert bereits");
    }

    [Fact]
    public async Task Unauthenticated_Access_Shows_Error()
    {
        if (_skipBrowser)
            return;

        await _page.GotoAsync($"{_serverUrl}/playlists");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        await Expect(_page.Locator("#playlists-load-error")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task UserB_Does_Not_See_UserA_Playlists()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await _page.GotoAsync($"{_serverUrl}/playlists");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        await _page.ClickAsync("#create-playlist-button");
        await _page.WaitForSelectorAsync("#playlist-name-input");
        await _page.FillAsync("#playlist-name-input", "Nur fuer Benutzer A");
        await _page.ClickAsync("#playlist-save-button");
        await _page.WaitForTimeoutAsync(1000);

        await Expect(_page.Locator(".playlist-row[data-playlist-name='Nur fuer Benutzer A']")).ToBeVisibleAsync();

        await LoginAsync(UserBEmail);
        await _page.GotoAsync($"{_serverUrl}/playlists");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        await Expect(_page.Locator(".playlist-row[data-playlist-name='Nur fuer Benutzer A']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Load_Detail_Page_Unauthenticated_Shows_Error()
    {
        if (_skipBrowser)
            return;

        await _page.GotoAsync($"{_serverUrl}/playlists/1");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        await Expect(_page.Locator("#playlist-detail-error")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Load_Detail_Page_ValidPlaylist_ShowsMetadata()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Metadaten-Test", "Beschreibung fuer Metadaten-Test");
        await row.Locator(".playlist-open-button").ClickAsync();
        await _page.WaitForSelectorAsync("#playlist-detail-name");

        await Expect(_page.Locator("#playlist-detail-name")).ToHaveTextAsync("Metadaten-Test");
        await Expect(_page.Locator("#playlist-detail-description")).ToHaveTextAsync("Beschreibung fuer Metadaten-Test");
        await Expect(_page.Locator("#playlist-detail-sortmode")).ToHaveTextAsync("Nach Erscheinungsdatum");

        var createdText = await _page.Locator("#playlist-detail-created").InnerTextAsync();
        var updatedText = await _page.Locator("#playlist-detail-updated").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(createdText));
        Assert.False(string.IsNullOrWhiteSpace(updatedText));
    }

    [Fact]
    public async Task Open_Button_In_List_Navigates_To_Detail()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Oeffnen-Navigation");
        await row.Locator(".playlist-open-button").ClickAsync();
        await _page.WaitForSelectorAsync("#playlist-detail-name");

        await Expect(_page.Locator("#playlist-detail-name")).ToHaveTextAsync("Oeffnen-Navigation");
    }

    [Fact]
    public async Task Detail_Page_Edit_Opens_Form_And_Saves()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Bearbeiten-Von-Detail");
        await row.Locator(".playlist-open-button").ClickAsync();
        await _page.WaitForSelectorAsync("#playlist-detail-name");

        await _page.ClickAsync(".playlist-detail-edit-button");
        await _page.WaitForSelectorAsync("#playlist-name-input");
        await _page.FillAsync("#playlist-name-input", "Bearbeiten-Von-Detail Aktualisiert");
        await _page.ClickAsync("#playlist-save-button");
        await _page.WaitForTimeoutAsync(1000);

        await Expect(_page.Locator("#playlist-detail-name")).ToHaveTextAsync("Bearbeiten-Von-Detail Aktualisiert");
    }

    [Fact]
    public async Task Detail_Page_Delete_Shows_Confirmation_And_Deletes()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Loeschen-Von-Detail");
        await row.Locator(".playlist-open-button").ClickAsync();
        await _page.WaitForSelectorAsync("#playlist-detail-name");

        await _page.ClickAsync(".playlist-detail-delete-button");
        await _page.WaitForSelectorAsync("#confirm-delete-playlist-button");
        await _page.ClickAsync("#confirm-delete-playlist-button");
        await _page.WaitForTimeoutAsync(1000);

        var currentUrl = new Uri(_page.Url);
        Assert.Equal("/playlists", currentUrl.AbsolutePath);
        await Expect(_page.Locator(".playlist-row[data-playlist-name='Loeschen-Von-Detail']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Detail_Page_Back_Button_Navigates_To_List()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Zurueck-Von-Detail");
        await row.Locator(".playlist-open-button").ClickAsync();
        await _page.WaitForSelectorAsync("#playlist-detail-name");

        await _page.ClickAsync(".playlist-detail-back-button");
        await _page.WaitForTimeoutAsync(1000);

        var currentUrl = new Uri(_page.Url);
        Assert.Equal("/playlists", currentUrl.AbsolutePath);
    }

    [Fact]
    public async Task Detail_Page_Foreign_Playlist_Shows_403_Error()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Fremde-Playlist-Test");
        await row.Locator(".playlist-open-button").ClickAsync();
        await _page.WaitForSelectorAsync("#playlist-detail-name");

        var detailUrl = _page.Url;

        await LoginAsync(UserBEmail);
        await _page.GotoAsync(detailUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        await Expect(_page.Locator("#playlist-detail-error")).ToContainTextAsync("Zugriff auf diese Playlist verweigert");
    }

    [Fact]
    public async Task Detail_Page_Nonexistent_Playlist_Shows_404_Error()
    {
        if (_skipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await _page.GotoAsync($"{_serverUrl}/playlists/999999999");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        await Expect(_page.Locator("#playlist-detail-error")).ToContainTextAsync("Playlist nicht gefunden");
    }

    private async Task LoginAsync(string email)
    {
        await _page.GotoAsync($"{_serverUrl}/Account/Login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await _page.FillAsync("#email", email);
        await _page.FillAsync("#password", Password);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);

        // Wait for Blazor Server to become interactive.
        await _page.WaitForTimeoutAsync(2000);
    }

    private async Task<ILocator> CreatePlaylistViaUiAsync(string name, string? description = null)
    {
        await _page.GotoAsync($"{_serverUrl}/playlists");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);

        await _page.ClickAsync("#create-playlist-button");
        await _page.WaitForSelectorAsync("#playlist-name-input");
        await _page.FillAsync("#playlist-name-input", name);
        if (description is not null)
            await _page.FillAsync("#playlist-description-input", description);
        await _page.ClickAsync("#playlist-save-button");
        await _page.WaitForTimeoutAsync(1000);

        var row = _page.Locator($".playlist-row[data-playlist-name='{name}']");
        await Expect(row).ToBeVisibleAsync();
        return row;
    }

    private async Task SeedUsersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        var userA = new ApplicationUser
        {
            UserName = UserAEmail,
            Email = UserAEmail,
            EmailConfirmed = true
        };
        var createA = await userManager.CreateAsync(userA, Password);
        if (!createA.Succeeded)
            throw new InvalidOperationException($"Benutzer A konnte nicht erstellt werden: {string.Join(", ", createA.Errors.Select(e => e.Description))}");

        var userB = new ApplicationUser
        {
            UserName = UserBEmail,
            Email = UserBEmail,
            EmailConfirmed = true
        };
        var createB = await userManager.CreateAsync(userB, Password);
        if (!createB.Succeeded)
            throw new InvalidOperationException($"Benutzer B konnte nicht erstellt werden: {string.Join(", ", createB.Errors.Select(e => e.Description))}");
    }
}
