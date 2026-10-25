using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright E2E tests for the ffmpeg/ffprobe download on <c>/admin/tv-sender</c>: the host
/// registers the real locators via their internal ctor against a temporary base directory
/// with all three lookup sources neutralized (no configured path, no <c>tools/ffmpeg/</c>,
/// no PATH), and <c>MediaProbe:DownloadSources:*</c> points at a Kestrel loopback package
/// server — so the download really installs into <c>tools/ffmpeg/</c> and the real
/// <c>Resolve()</c> finds the binaries afterwards.
/// </summary>
public sealed class TVSenderAdminE2ETests_FfmpegDownload : TVSenderAdminE2ETestBase
{
    private FfmpegPackageServer _packageServer = null!;
    private string _toolsBaseDir = null!;

    public override async ValueTask InitializeAsync()
    {
        var zipPayload = FfmpegToolDownloadTestSupport.CreateZipBytes(
            ("ffmpeg-8.0-essentials_build/bin/ffmpeg.exe", "FAKE-FFMPEG-BINARY"),
            ("ffmpeg-8.0-essentials_build/bin/ffprobe.exe", "FAKE-FFPROBE-BINARY"));
        var tarXzPayload = File.ReadAllBytes(
            FfmpegToolDownloadTestSupport.FixturePath("ffmpeg-release-amd64-static.tar.xz"));
        _packageServer = await FfmpegPackageServer.StartAsync(zipPayload, tarXzPayload);
        _toolsBaseDir = FfmpegToolDownloadTestSupport.NewTempBaseDir();
        await base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_packageServer is not null)
            await _packageServer.DisposeAsync();
    }

    protected override void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        builder.UseSetting("MediaProbe:DownloadSources:win-x64", _packageServer.ZipUrl);
        builder.UseSetting("MediaProbe:DownloadSources:linux-x64", _packageServer.TarXzUrl);
        builder.ConfigureServices(services =>
        {
            // Echte Locators mit allen drei Kandidatenquellen neutralisiert: frische
            // MediaProbeOptions ohne *Path, Temp-baseDirectory ohne tools/ffmpeg/,
            // pathEnvironment = null (ffmpeg liegt auf dem Testsystem im PATH).
            var locatorOptions = Options.Create(new MediaProbeOptions());
            services.AddSingleton<IFfprobeLocator>(
                new FfprobeLocator(locatorOptions, _toolsBaseDir, () => null));
            services.AddSingleton<IFfmpegLocator>(
                new FfmpegLocator(locatorOptions, _toolsBaseDir, () => null));
            services.AddSingleton<IFfmpegToolDownloadService>(sp => new FfmpegToolDownloadService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IOptions<MediaProbeOptions>>(),
                sp.GetRequiredService<ILogger<FfmpegToolDownloadService>>(),
                _toolsBaseDir,
                null));
        });
    }

    [Fact]
    public async Task Admin_DownloadsFfmpegTools_WhenMissing()
    {
        if (SkipBrowser) return;
        _packageServer.TransferDelayMilliseconds = 1500;
        await EnableTvSenderAsync();
        await OpenAdminPageAsAdminAsync();

        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");
        await Expect(Page.Locator("#tvsender-ffmpeg-missing")).ToBeVisibleAsync();
        await Expect(Page.Locator("#tvsender-ffmpeg-missing")).ToContainTextAsync("Live-Stream pausiert");

        await SwitchTabAsync("#tvsender-tab-laufzeit", "#tab-laufzeit");
        await Expect(Page.Locator("#tvsender-ffprobe-missing")).ToBeVisibleAsync();

        // GPL-/Lizenzhinweis vor dem Download.
        await Page.ClickAsync("#tvsender-download-tools-ffprobe");
        await Expect(Page.Locator("#tvsender-download-dialog")).ToBeVisibleAsync();
        await Expect(Page.Locator("#tvsender-download-dialog")).ToContainTextAsync("--enable-gpl");
        await Page.ClickAsync("#tvsender-download-confirm");

        // Waehrend der uebertragung (der Paketserver haelt die Antwort 1,5 s offen) sind
        // beide Buttons gesperrt und der Fortschritt sichtbar.
        await Expect(Page.Locator("#tvsender-download-progress-card")).ToBeVisibleAsync();
        await Expect(Page.Locator("#tvsender-download-tools-ffprobe")).ToBeDisabledAsync();
        await Expect(Page.Locator("#tvsender-download-tools-ffmpeg")).ToBeDisabledAsync();

        // Nach Erfolg verschwinden die Hinweise aus dem DOM — die echten Locators finden
        // die installierten Binaries ohne Neustart.
        await Page.Locator("#tvsender-ffprobe-missing").WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 30_000 });
        await Expect(Page.Locator("#tvsender-ffmpeg-missing")).ToHaveCountAsync(0);

        var toolDir = Path.Combine(_toolsBaseDir, "tools", "ffmpeg");
        var expectedNames = OperatingSystem.IsWindows()
            ? new[] { "ffmpeg.exe", "ffprobe.exe" }
            : new[] { "ffmpeg", "ffprobe" };
        foreach (var name in expectedNames)
            Assert.True(File.Exists(Path.Combine(toolDir, name)), $"{name} fehlt in {toolDir}");

        using var scope = Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFfprobeLocator>().Resolve());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFfmpegLocator>().Resolve());
    }

    [Fact]
    public async Task Admin_DownloadFails_ShowsErrorAlert()
    {
        if (SkipBrowser) return;
        _packageServer.FailWith404 = true;
        await EnableTvSenderAsync();
        await OpenAdminPageAsAdminAsync();

        await SwitchTabAsync("#tvsender-tab-laufzeit", "#tab-laufzeit");
        await Expect(Page.Locator("#tvsender-ffprobe-missing")).ToBeVisibleAsync();

        await Page.ClickAsync("#tvsender-download-tools-ffprobe");
        await Page.ClickAsync("#tvsender-download-confirm");

        await Expect(Page.Locator("#tvsender-download-error")).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
        await Expect(Page.Locator("#tvsender-download-error")).ToContainTextAsync("nicht heruntergeladen werden");
        await Expect(Page.Locator("#tvsender-download-error-close")).ToBeVisibleAsync();
        await Expect(Page.Locator("#tvsender-ffprobe-missing")).ToBeVisibleAsync();
    }
}
