using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using System.Net;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Pages;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit-Tests der Live-TV-Seite: Jetzt/Gleich/Vorschau-Darstellung, Sendelücke, Login-Formular
/// ohne Anmeldung, verständliche Fehlermeldungen und die JS-Anbindung des Live-Streams.
/// </summary>
public class LiveTvTests(ITestOutputHelper output)
{
    private const string TestToken = "test-token";

    private static BunitContext CreateContext(Mock<IProgramGuideApiClient>? guide = null, bool authorized = true)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        if (authorized)
            ctx.AddAuthorization().SetAuthorized("test-user");
        else
            ctx.AddAuthorization();

        ctx.Services.AddSingleton<VideoWebPlayerClient>(new TokenClient());
        ctx.Services.AddSingleton((guide ?? CreateGuideMock()).Object);
        ctx.Services.AddSingleton(new StaticAssetVersioner(new NullFileProvider()));
        return ctx;
    }

    private static Mock<IProgramGuideApiClient> CreateGuideMock(DtoProgramGuideNow? now = null, DtoProgramGuideEntry[]? upcoming = null)
    {
        var mock = new Mock<IProgramGuideApiClient>();
        mock.Setup(m => m.RequestProgramGuideNowAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(now ?? new DtoProgramGuideNow
            {
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Now = new DtoProgramGuideEntry
                {
                    SlotId = 11,
                    Title = "Laufender E2E-Film",
                    MediaItemId = 5,
                    StartUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
                    EndUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                    DurationSeconds = 3600,
                    PositionSeconds = 1800,
                    RemainingSeconds = 1800,
                },
                Next = new DtoProgramGuideEntry
                {
                    SlotId = 12,
                    Title = "Gleich-Komödie",
                    MediaItemId = 6,
                    StartUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                    EndUtc = DateTimeOffset.UtcNow.AddMinutes(120),
                    DurationSeconds = 5400,
                },
            });
        mock.Setup(m => m.RequestProgramGuideUpcomingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcoming ?? [
                new DtoProgramGuideEntry { SlotId = 11, Title = "Laufender E2E-Film", MediaItemId = 5, StartUtc = DateTimeOffset.UtcNow.AddMinutes(-30), EndUtc = DateTimeOffset.UtcNow.AddMinutes(30) },
                new DtoProgramGuideEntry { SlotId = 12, Title = "Gleich-Komödie", MediaItemId = 6, StartUtc = DateTimeOffset.UtcNow.AddMinutes(30), EndUtc = DateTimeOffset.UtcNow.AddMinutes(120) },
            ]);
        return mock;
    }

    /// <summary>Trägt einen Bearer-Token wie der impersonierte produktive Client.</summary>
    private sealed class TokenClient : VideoWebPlayerClient
    {
        public TokenClient() : base(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance)
        {
            SetAuthorizationToken(new AuthorizationToken { token = TestToken });
        }
    }

    [Fact]
    public void Now_LoadsAndRendersCurrentNextAndUpcoming_AndAttachesStream()
    {
        using var ctx = CreateContext();

        var cut = ctx.Render<LiveTv>();

        Assert.Equal("Laufender E2E-Film", cut.Find("#live-tv-now-title").TextContent.Trim());
        Assert.Contains("Gleich-Komödie", cut.Find("#live-tv-next").TextContent);
        Assert.Equal(2, cut.FindAll(".livetv-upcoming-row").Count);
        Assert.Single(cut.FindAll("#live-tv-video"));

        var attach = Assert.Single(ctx.JSInterop.Invocations.Where(i => i.Identifier == "liveTv.attach"));
        Assert.Contains(attach.Arguments, a => a is string s && s.Contains($"access_token={TestToken}"));
    }

    [Fact]
    public void Now_WithoutRunningSlot_ShowsNoProgramHintAndUpcoming_WithoutAttach()
    {
        var guide = CreateGuideMock(
            now: new DtoProgramGuideNow
            {
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Now = null,
                Next = new DtoProgramGuideEntry { SlotId = 12, Title = "Später-Film", MediaItemId = 6, StartUtc = DateTimeOffset.UtcNow.AddHours(1), EndUtc = DateTimeOffset.UtcNow.AddHours(2) },
            },
            upcoming: [new DtoProgramGuideEntry { SlotId = 12, Title = "Später-Film", MediaItemId = 6, StartUtc = DateTimeOffset.UtcNow.AddHours(1), EndUtc = DateTimeOffset.UtcNow.AddHours(2) }]);
        using var ctx = CreateContext(guide);

        var cut = ctx.Render<LiveTv>();

        Assert.Single(cut.FindAll("#live-tv-no-program"));
        Assert.Contains("Aktuell läuft keine Sendung", cut.Find("#live-tv-no-program").TextContent);
        Assert.Contains("Später-Film", cut.Find("#live-tv-next").TextContent);
        Assert.Single(cut.FindAll(".livetv-upcoming-row"));
        Assert.Empty(cut.FindAll("#live-tv-video"));
        Assert.Empty(ctx.JSInterop.Invocations.Where(i => i.Identifier == "liveTv.attach"));
    }

    [Fact]
    public void Page_NotAuthenticated_ShowsLoginForm_WithoutGuideCall()
    {
        var guide = CreateGuideMock();
        using var ctx = CreateContext(guide, authorized: false);

        var cut = ctx.Render<LiveTv>();

        Assert.Empty(cut.FindAll(".livetv-page"));
        guide.Verify(m => m.RequestProgramGuideNowAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Page_GuideCallFails_ShowsFriendlyLoadError()
    {
        var guide = new Mock<IProgramGuideApiClient>();
        guide.Setup(m => m.RequestProgramGuideNowAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom", null, HttpStatusCode.InternalServerError));
        using var ctx = CreateContext(guide);

        var cut = ctx.Render<LiveTv>();

        Assert.Contains("konnten nicht geladen werden", cut.Find("#live-tv-error").TextContent);
    }

    [Fact]
    public void Page_GuideCallForbidden_ShowsAccessHint()
    {
        var guide = new Mock<IProgramGuideApiClient>();
        guide.Setup(m => m.RequestProgramGuideNowAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("denied", null, HttpStatusCode.Forbidden));
        using var ctx = CreateContext(guide);

        var cut = ctx.Render<LiveTv>();

        Assert.Contains("keinen Zugriff auf den TV-Sender", cut.Find("#live-tv-error").TextContent);
    }

    [Fact]
    public async Task Page_NotifyStreamUnsupported_ShowsBrowserHint()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<LiveTv>();

        await cut.InvokeAsync(() => cut.Instance.NotifyStreamUnsupported());

        Assert.Contains("nicht unterstützt", cut.Find("#live-tv-unsupported").TextContent);
    }

    [Fact]
    public async Task Page_NotifyStreamError_ShowsRetryHint()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<LiveTv>();

        await cut.InvokeAsync(() => cut.Instance.NotifyStreamError("hls"));

        Assert.Contains("nicht verfügbar", cut.Find("#live-tv-stream-error").TextContent);
        Assert.Contains("erneut versucht", cut.Find("#live-tv-stream-error").TextContent);
    }
}
