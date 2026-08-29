using System.Text.RegularExpressions;
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

public sealed class MediaSourceSwitchE2ETests : IAsyncLifetime
{
    private const string UserEmail = "source-switcher@test.com";
    private const string UserPassword = "P@ssw0rd123!";
    private const string FirstSourceName = "Quelle Eins";
    private const string SecondSourceName = "Quelle Zwei";
    private const string FirstTitle = "Titel Quelle Eins";
    private const string SecondTitle = "Titel Quelle Zwei";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;
    private readonly List<string> _consoleMessages = [];
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private long _firstSourceId;
    private long _secondSourceId;
    private bool _skipBrowser;
    private string? _browserInfrastructureError;

    public MediaSourceSwitchE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-source-switch-e2e-{Guid.NewGuid()}.db");
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
    public async Task User_Can_Switch_Source_From_Menu_And_Sees_Only_Selected_Source_Titles()
    {
        if (_skipBrowser)
            Assert.Fail($"Playwright-/Browser-Infrastruktur ist nicht verfuegbar: {_browserInfrastructureError}");

        await LoginAsync();
        await _page.GotoAsync($"{_serverUrl}/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await OpenSourceFromMenuAsync(FirstSourceName, _firstSourceId);
        await Expect(_page.Locator("h1")).ToHaveTextAsync(FirstSourceName);
        await _page.WaitForSelectorAsync(".media-title-text", new() { State = WaitForSelectorState.Visible });
        await Expect(_page.Locator(".media-title-text")).ToContainTextAsync(FirstTitle);

        await OpenSourceFromMenuAsync(SecondSourceName, _secondSourceId);
        await Expect(_page.Locator("h1")).ToHaveTextAsync(SecondSourceName);
        await _page.WaitForSelectorAsync(".media-title-text", new() { State = WaitForSelectorState.Visible });
        await Expect(_page.Locator(".media-title-text")).ToContainTextAsync(SecondTitle);
        await Expect(_page.Locator(".media-title-text")).Not.ToContainTextAsync(FirstTitle);

        var severeErrors = _consoleMessages
            .Where(m => m.StartsWith("[page-error]") ||
                        (m.StartsWith("[response]") && (m.Contains(" 4") || m.Contains(" 5"))))
            .ToList();
        Assert.Empty(severeErrors);
    }

    private async Task LoginAsync()
    {
        await _page.GotoAsync($"{_serverUrl}/Account/Login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await _page.FillAsync("#email", UserEmail);
        await _page.FillAsync("#password", UserPassword);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);
    }

    private async Task OpenSourceFromMenuAsync(string sourceName, long sourceId)
    {
        var sidebar = _page.Locator("#sidebar");
        var sidebarClass = await sidebar.GetAttributeAsync("class");
        if (sidebarClass?.Contains("sidebar-closed", StringComparison.Ordinal) == true)
        {
            await _page.Locator("#sidebarToggle").ClickAsync();
            await Expect(sidebar).ToHaveClassAsync(new Regex("sidebar-open"));
        }

        var sourceLink = _page.Locator("nav .nav-link", new() { HasText = sourceName });
        await sourceLink.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        await sourceLink.ClickAsync();
        await Expect(_page).ToHaveURLAsync(new Regex($"/mediasource/{sourceId}$"));
    }

    private async Task SeedDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            UserName = UserEmail,
            Email = UserEmail,
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(user, UserPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException($"Testbenutzer konnte nicht erstellt werden: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

        var firstSource = CreateSource(FirstSourceName, "/source-one");
        var secondSource = CreateSource(SecondSourceName, "/source-two");
        db.MediaSources.AddRange(firstSource, secondSource);
        await db.SaveChangesAsync();

        _firstSourceId = firstSource.Id;
        _secondSourceId = secondSource.Id;

        db.MediaSourceUsers.AddRange(
            new MediaSourceUser { MediaSourceId = firstSource.Id, UserId = user.Id },
            new MediaSourceUser { MediaSourceId = secondSource.Id, UserId = user.Id });

        db.MovieCollections.AddRange(
            new MovieCollection { Name = FirstTitle, MediaSourceId = firstSource.Id },
            new MovieCollection { Name = SecondTitle, MediaSourceId = secondSource.Id });

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
