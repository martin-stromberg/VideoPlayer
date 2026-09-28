using System.IO.Compression;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using msTools.Backup;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class BackupUploadE2ETests : IAsyncLifetime
{
    private const string AdminEmail = "backup-admin@test.com";
    private const string AdminPassword = "P@ssw0rd123!";
    private const string UserEmail = "backup-user@test.com";
    private const string UserPassword = "P@ssw0rd456!";
    private const long MaxUploadSizeBytes = 4L * 1024 * 1024;

    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly string _uploadDir;
    private readonly WebApplicationFactory<global::Program> _factory;
    private readonly List<string> _consoleMessages = [];
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private string _serverUrl = null!;
    private bool _skipBrowser;
    private string? _browserInfrastructureError;

    public BackupUploadE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-backup-upload-e2e-{Guid.NewGuid()}.db");
        _backupDir = Path.Combine(Path.GetTempPath(), $"vwp-backup-upload-e2e-store-{Guid.NewGuid():N}");
        _uploadDir = Path.Combine(Path.GetTempPath(), $"vwp-backup-upload-e2e-upload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_backupDir);
        Directory.CreateDirectory(_uploadDir);
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
                builder.UseSetting("Backups:Path", _backupDir);
                builder.UseSetting("Backups:MaxUploadSizeBytes", MaxUploadSizeBytes.ToString());
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

        await SeedAdminAsync();
        await SeedRegularUserAsync();

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
        try { Directory.Delete(_backupDir, recursive: true); } catch { /* ignore */ }
        try { Directory.Delete(_uploadDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_ValidFile_ShowsProgressAndImportsBackup()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail, AdminPassword);
        var uploadPath = await CreateValidBackupFileAsync();
        var uploadFileName = Path.GetFileName(uploadPath);

        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await _page.Locator("input[type=file]").SetInputFilesAsync(uploadPath);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Backup hochladen" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/backups\\?backupStatus="));
        await Expect(_page.Locator(".alert-success")).ToContainTextAsync("Backup wurde importiert.");
        await Expect(_page.Locator(".backup-list-name").Filter(new() { HasText = uploadFileName })).ToHaveCountAsync(1);
        await Expect(_page.Locator(".backup-history-item strong").Filter(new() { HasText = "Upload" })).ToHaveCountAsync(1);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_LargeFile_UsesMultipleChunks()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail, AdminPassword);
        var uploadPath = await CreateValidBackupFileAsync();
        var chunkSize = Math.Max(1024, (int)(new FileInfo(uploadPath).Length / 3));

        var chunkRequests = 0;
        _page.Request += (_, request) =>
        {
            if (request.Url.Contains("/admin/backups/api/upload/chunk"))
                Interlocked.Increment(ref chunkRequests);
        };

        await _page.AddInitScriptAsync($"window.backupUploadChunkSizeBytes = {chunkSize};");
        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await _page.Locator("input[type=file]").SetInputFilesAsync(uploadPath);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Backup hochladen" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/backups\\?backupStatus="));
        Assert.True(chunkRequests >= 2, $"Expected multiple chunk requests, got {chunkRequests}.");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_Interrupted_ResumesFromServerOffset()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail, AdminPassword);
        var uploadPath = await CreateValidBackupFileAsync();
        var chunkSize = Math.Max(1024, (int)(new FileInfo(uploadPath).Length / 3));

        var chunkRequests = 0;
        _page.Request += (_, request) =>
        {
            if (request.Url.Contains("/admin/backups/api/upload/chunk"))
                Interlocked.Increment(ref chunkRequests);
        };

        var chunkAttempts = 0;
        await _page.RouteAsync("**/admin/backups/api/upload/chunk", async route =>
        {
            if (Interlocked.Increment(ref chunkAttempts) == 2)
            {
                await route.AbortAsync();
                await _page.UnrouteAsync("**/admin/backups/api/upload/chunk");
                return;
            }

            await route.ContinueAsync();
        });

        await _page.AddInitScriptAsync($"window.backupUploadChunkSizeBytes = {chunkSize};");
        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await _page.Locator("input[type=file]").SetInputFilesAsync(uploadPath);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Backup hochladen" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/backups\\?backupStatus="));
        await Expect(_page.Locator(".alert-success")).ToContainTextAsync("Backup wurde importiert.");
        Assert.True(chunkRequests >= 3, $"Expected the aborted chunk to be retried, got {chunkRequests} chunk requests.");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_InvalidFile_ShowsError()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail, AdminPassword);
        var invalidPath = Path.Combine(_uploadDir, $"invalid-{Guid.NewGuid():N}.bak");
        await using (var stream = new FileStream(invalidPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("data.txt");
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        }

        var invalidFileName = Path.GetFileName(invalidPath);

        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await _page.Locator("input[type=file]").SetInputFilesAsync(invalidPath);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Backup hochladen" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/backups\\?backupError="));
        await Expect(_page.Locator(".alert-danger")).ToContainTextAsync("manifest.json");
        Assert.Equal(0, await _page.Locator(".backup-list-name").Filter(new() { HasText = invalidFileName }).CountAsync());
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_ExceedingLimit_ShowsError()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail, AdminPassword);
        var bigPath = Path.Combine(_uploadDir, $"too-big-{Guid.NewGuid():N}.bak");
        await File.WriteAllBytesAsync(bigPath, new byte[MaxUploadSizeBytes + 1024], TestContext.Current.CancellationToken);

        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await _page.Locator("input[type=file]").SetInputFilesAsync(bigPath);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Backup hochladen" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/backups\\?backupError="));
        await Expect(_page.Locator(".alert-danger")).ToContainTextAsync("Upload-Limit");
        Assert.Equal(0, await _page.Locator(".backup-list-name").Filter(new() { HasText = Path.GetFileName(bigPath) }).CountAsync());
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_WhileRunning_DisablesUploadButton()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail, AdminPassword);
        var uploadPath = await CreateValidBackupFileAsync();

        var chunkGate = new TaskCompletionSource();
        await _page.RouteAsync("**/admin/backups/api/upload/chunk", async route =>
        {
            await chunkGate.Task;
            await route.ContinueAsync();
        });

        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await _page.Locator("input[type=file]").SetInputFilesAsync(uploadPath);
        var uploadButton = _page.GetByRole(AriaRole.Button, new() { Name = "Backup hochladen" });
        await uploadButton.ClickAsync();

        await Expect(uploadButton).ToBeDisabledAsync();

        chunkGate.SetResult();
        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/backups\\?backupStatus="));
        await Expect(_page.Locator(".alert-success")).ToContainTextAsync("Backup wurde importiert.");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_InterruptedSession_ShowsResumeHint()
    {
        EnsureBrowserAvailable();
        await _page.AddInitScriptAsync("localStorage.setItem('vwp-backup-upload:unterbrochen.bak:1024:1700000000000', '00000000-0000-0000-0000-000000000000');");
        await LoginAsync(AdminEmail, AdminPassword);

        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await Expect(_page.GetByText("unterbrochen.bak")).ToBeVisibleAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_InterruptedSession_CanDiscardResumeHint()
    {
        EnsureBrowserAvailable();
        await _page.AddInitScriptAsync("localStorage.setItem('vwp-backup-upload:unterbrochen.bak:1024:1700000000000', '00000000-0000-0000-0000-000000000000');");
        await LoginAsync(AdminEmail, AdminPassword);

        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await Expect(_page.GetByText("unterbrochen.bak")).ToBeVisibleAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Verwerfen" }).ClickAsync();

        await Expect(_page.GetByText("unterbrochen.bak")).ToHaveCountAsync(0);
        var remaining = await _page.EvaluateAsync<int>(
            "() => Object.keys(localStorage).filter(k => k.startsWith('vwp-backup-upload:')).length");
        Assert.Equal(0, remaining);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_NonJsonErrorResponse_ShowsFriendlyMessage()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail, AdminPassword);
        var uploadPath = await CreateValidBackupFileAsync();

        await _page.RouteAsync("**/admin/backups/api/upload/chunk", route =>
            route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 413,
                ContentType = "text/html",
                Body = "<html><body>Request entity too large</body></html>"
            }));

        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await _page.Locator("input[type=file]").SetInputFilesAsync(uploadPath);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Backup hochladen" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/backups\\?backupError="));
        await Expect(_page.Locator(".alert-danger")).ToContainTextAsync("Serverlimit");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_NavigatingAwayDuringUpload_AbortsWithoutRedirectBack()
    {
        EnsureBrowserAvailable();
        await LoginAsync(AdminEmail, AdminPassword);
        var uploadPath = await CreateValidBackupFileAsync();

        var chunkSeen = new TaskCompletionSource();
        var chunkGate = new TaskCompletionSource();
        await _page.RouteAsync("**/admin/backups/api/upload/chunk", async route =>
        {
            chunkSeen.TrySetResult();
            await chunkGate.Task;
            try
            {
                await route.ContinueAsync();
            }
            catch (PlaywrightException)
            {
                // The upload was aborted client-side; continuing the route may fail.
            }
        });

        await _page.GotoAsync($"{_serverUrl}/admin/backups");
        await WaitForInteractivePageAsync();

        await _page.Locator("input[type=file]").SetInputFilesAsync(uploadPath);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Backup hochladen" }).ClickAsync();
        await chunkSeen.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Programmatic click on the nav link triggers Blazor enhanced navigation
        // (client-side DOM patch, no script reload) even while the sidebar is collapsed.
        await _page.EvaluateAsync(
            "() => { const link = [...document.querySelectorAll('a.nav-link')].find(a => a.textContent.includes('Startseite')); if (link) link.click(); }");
        await Expect(_page).ToHaveURLAsync($"{_serverUrl}/");

        chunkGate.SetResult();
        await _page.WaitForTimeoutAsync(2_000);

        Assert.Equal($"{_serverUrl}/", _page.Url);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task BackupUpload_NonAdmin_SeesNoUploadControl()
    {
        EnsureBrowserAvailable();
        await LoginAsync(UserEmail, UserPassword);

        await _page.GotoAsync($"{_serverUrl}/admin/backups");

        await Expect(_page.GetByText("Nicht autorisiert.")).ToBeVisibleAsync();
        Assert.Equal(0, await _page.Locator("input[type=file]").CountAsync());
        Assert.Equal(0, await _page.GetByRole(AriaRole.Button, new() { Name = "Backup hochladen" }).CountAsync());
    }

    private void EnsureBrowserAvailable()
    {
        if (_skipBrowser)
            Assert.Fail($"Playwright-/Browser-Infrastruktur ist nicht verfuegbar: {_browserInfrastructureError}\n{string.Join("\n", _consoleMessages)}");
    }

    private async Task LoginAsync(string email, string password)
    {
        await _page.GotoAsync($"{_serverUrl}/Account/Login");
        await _page.FillAsync("#email", email);
        await _page.FillAsync("#password", password);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.Load);
    }

    private async Task WaitForInteractivePageAsync()
    {
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "System Backups" })).ToBeVisibleAsync();
        await _page.WaitForTimeoutAsync(1_000);
    }

    private async Task<string> CreateValidBackupFileAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<IBackupDataSource>();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

        var items = await dataSource.GetBackupDataAsync(TestContext.Current.CancellationToken);
        var result = await backupService.StoreAsync(
            $"seed-{Guid.NewGuid():N}",
            BackupGeneration.Manual,
            items,
            TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, result.Message);

        var uploadPath = Path.Combine(_uploadDir, $"upload-{Guid.NewGuid():N}.bak");
        File.Copy(result.BackupPath, uploadPath);
        return uploadPath;
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
