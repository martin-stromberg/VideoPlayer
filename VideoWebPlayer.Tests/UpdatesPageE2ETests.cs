using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using msTools.Updater;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Updates;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class UpdatesPageE2ETests : IAsyncLifetime
{
    private const string AdminEmail = "updates-admin@test.com";
    private const string AdminPassword = "P@ssw0rd123!";

    private readonly string _dbPath;
    private readonly FakeUpdateBackend _updates = new();
    private readonly WebApplicationFactory<global::Program> _factory;
    private readonly List<string> _consoleMessages = [];
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private bool _skipBrowser;
    private string? _browserInfrastructureError;

    public UpdatesPageE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-updates-e2e-{Guid.NewGuid()}.db");
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
                    services.RemoveAll<IUpdateSettingsService>();
                    services.RemoveAll<UpdateSettingsService>();
                    services.RemoveAll<IAutoUpdateOrchestrator>();
                    services.RemoveAll<IAutoUpdateCommandHandler>();
                    services.AddSingleton(_updates);
                    services.AddScoped<IUpdateSettingsService>(sp => sp.GetRequiredService<FakeUpdateBackend>());
                    services.AddScoped<IAutoUpdateOrchestrator>(sp => sp.GetRequiredService<FakeUpdateBackend>());
                    services.AddScoped<IAutoUpdateCommandHandler>(sp => sp.GetRequiredService<FakeUpdateBackend>());
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

        await SeedAdminAsync();

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
    public async Task Admin_Navigates_To_Updates_Page_And_Sees_Structured_Status()
    {
        EnsureBrowserAvailable();
        await LoginAsync();

        await _page.GotoAsync($"{_serverUrl}/admin");
        await _page.GetByRole(AriaRole.Link, new() { Name = "Updates Versionen pruefen, installieren und absichern" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/updates$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Systemupdates" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Update-Status" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Versionsdetails" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Konfiguration" })).ToBeVisibleAsync();
        await Expect(_page.Locator("[data-testid='update-state']")).ToHaveTextAsync("Wartet");
        await Expect(MetricValue("Installiert")).ToHaveTextAsync("1.0.0");
        await Expect(MetricValue("Verfuegbar")).ToHaveTextAsync("-");
        await Expect(MetricValue("Letzte Pruefung")).Not.ToHaveTextAsync("-");
        await Expect(_page.Locator("[data-testid='version-details-section']")).ToContainTextAsync("Noch kein Download vorhanden.");
        await Expect(_page.Locator("[data-testid='version-details-section']")).ToContainTextAsync("Prerelease-Kanal");
        await Expect(_page.Locator("[data-testid='version-details-section']")).ToContainTextAsync("Release-Datum");
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Update installieren" })).ToBeDisabledAsync();

        _updates.Status = FakeUpdateBackend.CreateStatus(AutoUpdateState.UpdateAvailable, availableVersion: "1.2.0");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Daten aktualisieren" }).ClickAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Update installieren" })).ToBeEnabledAsync();

        _updates.Status = FakeUpdateBackend.CreateStatus((AutoUpdateState)999);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Daten aktualisieren" }).ClickAsync();
        await Expect(_page.Locator("[data-testid='update-state']")).ToHaveTextAsync("Unbekannt");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Admin_Checks_For_Updates_And_Refreshes_Changed_Data()
    {
        EnsureBrowserAvailable();
        await LoginAsync();
        await _page.GotoAsync($"{_serverUrl}/admin/updates");
        await WaitForInteractivePageAsync();

        var initialCheckedAt = await MetricValue("Letzte Pruefung").TextContentAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Nach Updates suchen" }).ClickAsync();
        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/updates\\?checkStatus="));
        await Expect(_page.Locator("[data-testid='update-status-section']")).ToContainTextAsync("Test-Pruefung abgeschlossen.");
        await Expect(_page.Locator(".admin-update-page > .alert").Filter(new() { HasText = "Test-Pruefung abgeschlossen." })).ToHaveCountAsync(0);
        await Expect(_page.Locator("[data-testid='version-details-section']")).ToContainTextAsync("1.1.0");
        Assert.Equal(1, _updates.CheckCount);
        var refreshedCheckedAt = await MetricValue("Letzte Pruefung").TextContentAsync();
        Assert.NotEqual(initialCheckedAt, refreshedCheckedAt);

        _updates.Status = FakeUpdateBackend.CreateStatus(AutoUpdateState.UpdateAvailable, availableVersion: "2.4.0");
        await _page.GetByLabel("Dienstname fuer Neustart").FillAsync("ungespeicherter-dienst");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Daten aktualisieren" }).ClickAsync();

        await Expect(_page.Locator("[data-testid='version-details-section']")).ToContainTextAsync("2.4.0");
        Assert.Equal(0, _updates.SaveCount);

        _updates.Status = FakeUpdateBackend.CreateStatus(
            AutoUpdateState.Failed,
            lastError: "GitHub-Releases konnten nicht geladen werden.",
            lastErrorCode: AutoUpdateErrorCode.SourceUnavailable);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Daten aktualisieren" }).ClickAsync();
        await Expect(_page.GetByText("Fehler beim Abruf")).ToBeVisibleAsync();
        await Expect(_page.GetByText("GitHub-Releases konnten nicht geladen werden.")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Code: SourceUnavailable")).ToBeVisibleAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Admin_Edits_Resets_And_Saves_Update_Configuration()
    {
        EnsureBrowserAvailable();
        await LoginAsync();
        await _page.GotoAsync($"{_serverUrl}/admin/updates");
        await WaitForInteractivePageAsync();

        await _page.GetByLabel("Pruefintervall in Minuten").FillAsync("0");
        await _page.GetByLabel("Aufzubewahrende Update-Backups").FillAsync("11");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Konfiguration speichern" }).ClickAsync();
        await Expect(_page.GetByText("Das Pruefintervall muss zwischen 1 und 1440 Minuten liegen.").First).ToBeVisibleAsync();
        await Expect(_page.GetByText("Es koennen 1 bis 10 Update-Backups aufbewahrt werden.").First).ToBeVisibleAsync();
        Assert.Equal(0, _updates.SaveCount);

        await _page.GetByLabel("Automatische Pruefung").UncheckAsync();
        await _page.GetByLabel("Vorabversionen akzeptieren").CheckAsync();
        await _page.GetByLabel("Automatische Installation").CheckAsync();
        await _page.GetByLabel("Backup vor Installation").UncheckAsync();
        await _page.GetByLabel("Bei Backupfehler abbrechen").UncheckAsync();
        await _page.GetByLabel("Pruefintervall in Minuten").FillAsync("45");
        await _page.GetByLabel("Dienstname fuer Neustart").FillAsync("abweichender-dienst");
        await _page.GetByLabel("Update-Backup-Pfad").FillAsync("AbweichendeBackups");
        await _page.GetByLabel("Aufzubewahrende Update-Backups").FillAsync("7");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Standards zuruecksetzen" }).ClickAsync();

        await Expect(_page.GetByLabel("Automatische Pruefung")).ToBeCheckedAsync();
        await Expect(_page.GetByLabel("Vorabversionen akzeptieren")).Not.ToBeCheckedAsync();
        await Expect(_page.GetByLabel("Automatische Installation")).Not.ToBeCheckedAsync();
        await Expect(_page.GetByLabel("Backup vor Installation")).ToBeCheckedAsync();
        await Expect(_page.GetByLabel("Bei Backupfehler abbrechen")).ToBeCheckedAsync();
        await Expect(_page.GetByLabel("Pruefintervall in Minuten")).ToHaveValueAsync("360");
        await Expect(_page.GetByLabel("Dienstname fuer Neustart")).ToHaveValueAsync("standard-dienst");
        await Expect(_page.GetByLabel("Update-Backup-Pfad")).ToHaveValueAsync("StandardBackups");
        await Expect(_page.GetByLabel("Aufzubewahrende Update-Backups")).ToHaveValueAsync("5");
        Assert.Equal(0, _updates.SaveCount);

        await _page.GetByLabel("Vorabversionen akzeptieren").CheckAsync();
        await Expect(_page.GetByText("Vorabversionen koennen experimentell sein.")).ToBeVisibleAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Konfiguration speichern" }).ClickAsync();
        await Expect(_page.GetByText("Prerelease-Versionen wurden nicht aktiviert, weil die Sicherheitsabfrage nicht bestaetigt wurde.")).ToBeVisibleAsync();
        await Expect(_page.GetByLabel("Vorabversionen akzeptieren")).Not.ToBeCheckedAsync();
        Assert.Equal(0, _updates.SaveCount);

        await _page.GetByLabel("Automatische Pruefung").UncheckAsync();
        await _page.GetByLabel("Vorabversionen akzeptieren").CheckAsync();
        await _page.GetByLabel("Prerelease-Aktivierung bestaetigen").CheckAsync();
        await _page.GetByLabel("Automatische Installation").CheckAsync();
        await _page.GetByLabel("Backup vor Installation").UncheckAsync();
        await _page.GetByLabel("Bei Backupfehler abbrechen").UncheckAsync();
        await _page.GetByLabel("Pruefintervall in Minuten").FillAsync("30");
        await _page.GetByLabel("Dienstname fuer Neustart").FillAsync("gespeicherter-dienst");
        await _page.GetByLabel("Update-Backup-Pfad").FillAsync("GespeicherteBackups");
        await _page.GetByLabel("Aufzubewahrende Update-Backups").FillAsync("4");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Konfiguration speichern" }).ClickAsync();

        await Expect(_page.GetByText("Konfiguration wurde gespeichert.")).ToBeVisibleAsync();
        Assert.Equal(1, _updates.SaveCount);
        Assert.False(_updates.Settings.AutomaticChecksEnabled);
        Assert.True(_updates.Settings.AllowPrereleaseUpdates);
        Assert.True(_updates.Settings.AutomaticInstallationEnabled);
        Assert.True(_updates.Settings.AutomaticDownloadEnabled);
        Assert.False(_updates.Settings.CreateBackupBeforeInstallation);
        Assert.False(_updates.Settings.CancelInstallationOnBackupFailure);
        Assert.Equal(30, _updates.Settings.CheckIntervalMinutes);
        Assert.Equal("gespeicherter-dienst", _updates.Settings.ServiceName);
        Assert.Equal("GespeicherteBackups", _updates.Settings.UpdateBackupPath);
        Assert.Equal(4, _updates.Settings.RetainedUpdateBackupCount);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Admin_Installs_Update_Through_Post_And_Sees_Result()
    {
        EnsureBrowserAvailable();
        await LoginAsync();
        _updates.Status = FakeUpdateBackend.CreateStatus(AutoUpdateState.UpdateAvailable, availableVersion: "3.0.0");
        await _page.GotoAsync($"{_serverUrl}/admin/updates");
        await WaitForInteractivePageAsync();

        await _page.GetByRole(AriaRole.Button, new() { Name = "Update installieren" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/updates\\?updateStatus="));
        await Expect(_page.GetByText("Installation gestartet.")).ToBeVisibleAsync();
        Assert.Equal(1, _updates.DownloadCount);
        Assert.Equal(1, _updates.InstallCount);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Updates_Page_Disables_Manual_Actions_When_Busy_Or_Locked()
    {
        EnsureBrowserAvailable();
        await LoginAsync();
        await _page.GotoAsync($"{_serverUrl}/admin/updates");
        await WaitForInteractivePageAsync();

        foreach (var state in new[] { AutoUpdateState.Checking, AutoUpdateState.Downloading, AutoUpdateState.Installing })
        {
            _updates.Status = FakeUpdateBackend.CreateStatus(state, availableVersion: "2.0.0");
            await _page.GetByRole(AriaRole.Button, new() { Name = "Daten aktualisieren" }).ClickAsync();
            await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Nach Updates suchen" })).ToBeDisabledAsync();
            await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Update installieren" })).ToBeDisabledAsync();
        }

        _updates.Status = FakeUpdateBackend.CreateStatus(
            AutoUpdateState.UpdateAvailable,
            availableVersion: "2.0.0",
            isLocked: true,
            lockCreatedAt: new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));
        await _page.GetByRole(AriaRole.Button, new() { Name = "Daten aktualisieren" }).ClickAsync();

        await Expect(_page.GetByText("Update-Sperre aktiv")).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Nach Updates suchen" })).ToBeDisabledAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Update installieren" })).ToBeDisabledAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Updates_Page_Remains_Usable_On_Mobile_Viewport()
    {
        EnsureBrowserAvailable();
        await LoginAsync();
        await _page.SetViewportSizeAsync(375, 812);
        await _page.GotoAsync($"{_serverUrl}/admin/updates");
        await WaitForInteractivePageAsync();

        await AssertKeyControlsVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Update-Status" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Versionsdetails" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Konfiguration" })).ToBeVisibleAsync();
        await AssertNoHorizontalOverflowAsync();
        await AssertNoVisibleTextOverflowAsync();
        await _page.ScreenshotAsync(new() { FullPage = true });
        await AssertFocusOrderAsync();

        await _page.SetViewportSizeAsync(1280, 900);
        await _page.GotoAsync($"{_serverUrl}/admin/updates");
        await WaitForInteractivePageAsync();

        await AssertKeyControlsVisibleAsync();
        await AssertNoHorizontalOverflowAsync();
        await AssertNoVisibleTextOverflowAsync();
        await AssertNoPanelOverlapAsync();
        await _page.ScreenshotAsync(new() { FullPage = true });
        Assert.Empty(SevereErrors());
    }

    private ILocator MetricValue(string label)
        => _page.Locator($"xpath=//dt[normalize-space()='{label}']/following-sibling::dd[1]");

    private void EnsureBrowserAvailable()
    {
        if (_skipBrowser)
            Assert.Fail($"Playwright-/Browser-Infrastruktur ist nicht verfuegbar: {_browserInfrastructureError}");
    }

    private async Task LoginAsync()
    {
        await _page.GotoAsync($"{_serverUrl}/Account/Login");
        await _page.FillAsync("#email", AdminEmail);
        await _page.FillAsync("#password", AdminPassword);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);
    }

    private async Task WaitForInteractivePageAsync()
    {
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Systemupdates" })).ToBeVisibleAsync();
        await _page.WaitForTimeoutAsync(1_000);
    }

    private async Task AssertKeyControlsVisibleAsync()
    {
        foreach (var label in new[]
        {
            "Automatische Pruefung",
            "Pruefintervall in Minuten",
            "Vorabversionen akzeptieren",
            "Automatische Installation",
            "Dienstname fuer Neustart",
            "Backup vor Installation",
            "Bei Backupfehler abbrechen",
            "Update-Backup-Pfad",
            "Aufzubewahrende Update-Backups"
        })
        {
            await Expect(_page.GetByLabel(label)).ToBeVisibleAsync();
        }

        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Nach Updates suchen" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Daten aktualisieren" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Update installieren" })).ToBeVisibleAsync();
    }

    private async Task AssertNoHorizontalOverflowAsync()
    {
        var hasHorizontalOverflow = await _page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(hasHorizontalOverflow);
    }

    private async Task AssertNoVisibleTextOverflowAsync()
    {
        var overflowingText = await _page.EvaluateAsync<string[]>(
            @"() => Array.from(document.querySelectorAll(
                '.admin-update-page button, .admin-update-page label, .admin-update-page h1, .admin-update-page h2, .update-metric-list dd, .update-detail-grid dd'))
                .filter(el => el.offsetParent !== null && el.scrollWidth > el.clientWidth + 1)
                .map(el => (el.textContent || el.getAttribute('aria-label') || el.id || el.tagName).trim())
                .filter(Boolean)");
        Assert.Empty(overflowingText);
    }

    private async Task AssertNoPanelOverlapAsync()
    {
        var overlaps = await _page.EvaluateAsync<string[]>(
            @"() => {
                const panels = Array.from(document.querySelectorAll('.update-overview-grid > .update-panel, .update-config-panel'))
                    .filter(el => el.offsetParent !== null)
                    .map((el, index) => ({ index, rect: el.getBoundingClientRect() }));
                const overlapPairs = [];
                for (let i = 0; i < panels.length; i++) {
                    for (let j = i + 1; j < panels.length; j++) {
                        const a = panels[i].rect;
                        const b = panels[j].rect;
                        const overlaps = a.left < b.right - 1 && a.right > b.left + 1 && a.top < b.bottom - 1 && a.bottom > b.top + 1;
                        if (overlaps) {
                            overlapPairs.push(`${panels[i].index}:${panels[j].index}`);
                        }
                    }
                }
                return overlapPairs;
            }");
        Assert.Empty(overlaps);
    }

    private async Task AssertFocusOrderAsync()
    {
        var focusableNames = await _page.EvaluateAsync<string[]>(
            @"() => Array.from(document.querySelectorAll('.admin-update-page button:not([disabled]), .admin-update-page input:not([disabled])'))
                .map(el => {
                    if (el.tagName === 'BUTTON') return el.textContent.trim();
                    const label = document.querySelector(`label[for='${el.id}']`);
                    return label ? label.textContent.trim().replace(/\s+/g, ' ') : el.id;
                })");

        Assert.Contains("Nach Updates suchen", focusableNames);
        Assert.Contains("Daten aktualisieren", focusableNames);
        Assert.True(focusableNames.IndexOf("Automatische Pruefung Regelmaessig nach neuen Versionen suchen.") < focusableNames.IndexOf("Pruefintervall in Minuten"));
        Assert.True(focusableNames.IndexOf("Vorabversionen akzeptieren Prerelease-Versionen in die Pruefung einbeziehen.") < focusableNames.IndexOf("Automatische Installation Gefundene Updates automatisch installieren."));
        Assert.True(focusableNames.IndexOf("Update-Backup-Pfad") < focusableNames.IndexOf("Aufzubewahrende Update-Backups"));
        Assert.True(focusableNames.IndexOf("Standards zuruecksetzen") < focusableNames.IndexOf("Konfiguration speichern"));
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

    private IReadOnlyList<string> SevereErrors()
        => _consoleMessages
            .Where(m => m.StartsWith("[page-error]") ||
                        (m.StartsWith("[response]") && (m.Contains(" 4") || m.Contains(" 5"))))
            .ToList();

    private sealed class FakeUpdateBackend : IUpdateSettingsService, IAutoUpdateOrchestrator, IAutoUpdateCommandHandler
    {
        public UpdateSettings Settings { get; private set; } = new()
        {
            AutomaticChecksEnabled = true,
            CheckIntervalMinutes = 60,
            AllowPrereleaseUpdates = false,
            AutomaticInstallationEnabled = false,
            AutomaticDownloadEnabled = true,
            ServiceName = "aktueller-dienst",
            CreateBackupBeforeInstallation = true,
            CancelInstallationOnBackupFailure = true,
            UpdateBackupPath = "AktuelleBackups",
            RetainedUpdateBackupCount = 3
        };

        public AutoUpdateStatusSnapshot Status { get; set; } = CreateStatus(AutoUpdateState.Idle);

        public int CheckCount { get; private set; }

        public int SaveCount { get; private set; }

        public int DownloadCount { get; private set; }

        public int InstallCount { get; private set; }

        public UpdateSettings GetDefaultSettings()
            => new()
            {
                AutomaticChecksEnabled = true,
                CheckIntervalMinutes = 360,
                AllowPrereleaseUpdates = false,
                AutomaticInstallationEnabled = false,
                AutomaticDownloadEnabled = true,
                ServiceName = "standard-dienst",
                CreateBackupBeforeInstallation = true,
                CancelInstallationOnBackupFailure = true,
                UpdateBackupPath = "StandardBackups",
                RetainedUpdateBackupCount = 5
            };

        public Task<UpdateSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Settings);

        public Task<UpdateSettings> UpdateAsync(UpdateSettingsUpdate update, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            Settings = new UpdateSettings
            {
                AutomaticChecksEnabled = update.AutomaticChecksEnabled,
                CheckIntervalMinutes = update.CheckIntervalMinutes,
                AllowPrereleaseUpdates = update.AllowPrereleaseUpdates,
                AutomaticInstallationEnabled = update.AutomaticInstallationEnabled,
                AutomaticDownloadEnabled = update.AutomaticInstallationEnabled || update.AutomaticDownloadEnabled,
                ServiceName = string.IsNullOrWhiteSpace(update.ServiceName) ? null : update.ServiceName.Trim(),
                CreateBackupBeforeInstallation = update.CreateBackupBeforeInstallation,
                CancelInstallationOnBackupFailure = update.CancelInstallationOnBackupFailure,
                UpdateBackupPath = string.IsNullOrWhiteSpace(update.UpdateBackupPath) ? "StandardBackups" : update.UpdateBackupPath.Trim(),
                RetainedUpdateBackupCount = update.RetainedUpdateBackupCount
            };
            return Task.FromResult(Settings);
        }

        public Task ApplyToRuntimeOptionsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<UpdateBackupOptions> GetBackupOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UpdateBackupOptions
            {
                Enabled = Settings.CreateBackupBeforeInstallation,
                Path = Settings.UpdateBackupPath,
                RetainedBackupCount = Settings.RetainedUpdateBackupCount,
                CancelInstallationOnFailure = Settings.CancelInstallationOnBackupFailure
            });

        public Task<AutoUpdateStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Status);

        public Task<AutoUpdateResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
            => CheckAsync(cancellationToken);

        public Task<AutoUpdateResult> RunUpdateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "Update abgeschlossen.", null!));

        public Task<AutoUpdateResult> CheckAsync(CancellationToken cancellationToken = default)
        {
            CheckCount++;
            Status = CreateStatus(
                AutoUpdateState.Success,
                availableVersion: "1.1.0",
                lastCheckedAt: new DateTimeOffset(2026, 8, 29, 11, 45, 0, TimeSpan.Zero));
            return Task.FromResult(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "Test-Pruefung abgeschlossen.", null!));
        }

        public Task<AutoUpdateResult> DownloadAsync(CancellationToken cancellationToken = default)
        {
            DownloadCount++;
            Status = CreateStatus(AutoUpdateState.ReadyToInstall, availableVersion: Status.AvailableVersion ?? Status.LastCheckResult?.AvailableVersion);
            return Task.FromResult(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.ReadyToInstall, "Download abgeschlossen.", null!));
        }

        public Task<AutoUpdateResult> InstallAsync(bool restartService, bool force, CancellationToken cancellationToken = default)
        {
            InstallCount++;
            Status = CreateStatus(AutoUpdateState.Installing, availableVersion: Status.AvailableVersion ?? Status.LastCheckResult?.AvailableVersion);
            return Task.FromResult(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Installing, "Installation gestartet.", null!));
        }

        public static AutoUpdateStatusSnapshot CreateStatus(
            AutoUpdateState state,
            string? availableVersion = null,
            DateTimeOffset? lastCheckedAt = null,
            DateTimeOffset? publishedAt = null,
            bool isPrerelease = false,
            string? lastError = null,
            AutoUpdateErrorCode? lastErrorCode = null,
            bool isLocked = false,
            DateTimeOffset? lockCreatedAt = null)
            => new(
                state,
                InstalledVersion: "1.0.0",
                AvailableVersion: availableVersion,
                LastCheckedAt: lastCheckedAt ?? new DateTimeOffset(2026, 8, 29, 10, 15, 0, TimeSpan.Zero),
                LastCheckResult: availableVersion is null
                    ? null!
                    : new AutoUpdateCheckResult(
                        availableVersion,
                        null!,
                        null,
                        publishedAt ?? new DateTimeOffset(2026, 8, 29, 9, 0, 0, TimeSpan.Zero),
                        isPrerelease),
                LastDownloadResult: null!,
                LastInstallResult: null!,
                LastError: lastError,
                LastErrorCode: lastErrorCode,
                IsLocked: isLocked,
                LockCreatedAt: isLocked ? lockCreatedAt ?? new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero) : null);
    }
}
