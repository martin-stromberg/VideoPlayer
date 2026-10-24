using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using System.Globalization;
using System.Net;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Pages;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit-Tests der Live-TV-Seite: Jetzt/Gleich/Vorschau-Darstellung, Sendelücke, Login-Formular
/// ohne Anmeldung, verständliche Fehlermeldungen und die JS-Anbindung des Live-Streams.
/// </summary>
public class LiveTvTests
{
    private const string TestToken = "test-token";
    private static readonly Helpers.ControllableTimeProvider ControllableTime = new();

    private const string TestUserId = "test-user-id";

    private static BunitContext CreateContext(Mock<IProgramGuideApiClient>? guide = null, bool authorized = true, bool tvSenderEnabled = true, bool tvSenderAccessGranted = true)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        if (authorized)
            ctx.AddAuthorization().SetAuthorized("test-user")
                .SetClaims(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, TestUserId));
        else
            ctx.AddAuthorization();

        ctx.Services.AddSingleton<VideoWebPlayerClient>(new TokenClient());
        ctx.Services.AddSingleton((guide ?? CreateGuideMock()).Object);
        ctx.Services.AddSingleton(new StaticAssetVersioner(new NullFileProvider()));
        ctx.Services.AddSingleton<TimeProvider>(ControllableTime);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, new EventManager());
        db.Setups.Add(new Setup { TvSenderEnabled = tvSenderEnabled });
        if (tvSenderAccessGranted)
            db.TvSenderUserAccess.Add(new TvSenderUserAccess { UserId = TestUserId });
        db.SaveChanges();
        ctx.Services.AddSingleton(new ProgramSettingsService(db, NullLogger<ProgramSettingsService>.Instance));
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

        var attach = Assert.Single(ctx.JSInterop.Invocations, i => i.Identifier == "liveTv.attach");
        Assert.Contains(attach.Arguments, a => a is string s && s.Contains($"access_token={TestToken}"));
    }

    [Fact]
    public void Now_ProgressFill_WritesInvariantPercent_UnderGermanCulture()
    {
        // N26: "width:59,38%" (de-DE-Komma) ist ungueltiges CSS und laesst den Balken immer
        // voll wirken — der Wert muss mit Punkt-Dezimaltrenner serialisiert werden.
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            var guide = CreateGuideMock(
                now: new DtoProgramGuideNow
                {
                    ServerTimeUtc = DateTimeOffset.UtcNow,
                    Now = new DtoProgramGuideEntry
                    {
                        SlotId = 11,
                        Title = "Halbzeit",
                        MediaItemId = 5,
                        StartUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                        EndUtc = DateTimeOffset.UtcNow.AddMinutes(70),
                        // 450s von 3600s = 12,5 % — gebrochener Wert, der unter de-DE ein
                        // Komma produzieren wuerde, wenn die Formatierung kulturabhaengig waere.
                        DurationSeconds = 3600,
                        PositionSeconds = 450,
                        RemainingSeconds = 3150,
                    },
                });
            using var ctx = CreateContext(guide);

            var cut = ctx.Render<LiveTv>();

            var fill = cut.Find(".progress-fill");
            var style = fill.GetAttribute("style");
            Assert.Equal("width:12.5%", style);
            Assert.DoesNotContain(',', style!);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
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
        Assert.DoesNotContain(ctx.JSInterop.Invocations, i => i.Identifier == "liveTv.attach");
    }

    [Fact]
    public void Upcoming_RendersCompactProgramCards_WithPosterBadgeAndDescription()
    {
        var guide = CreateGuideMock(
            now: new DtoProgramGuideNow
            {
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Now = new DtoProgramGuideEntry
                {
                    SlotId = 11,
                    Title = "Krimiserie — Staffel 1, E2: Der Fall",
                    ShowName = "Krimiserie",
                    SeasonName = "Staffel 1",
                    SeasonNumber = 1,
                    EpisodeNumber = 2,
                    EpisodeName = "Der Fall",
                    Description = "Episodentext.",
                    GenreName = "Krimi",
                    PosterPictureId = 42,
                    MediaItemId = 5,
                    StartUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                    EndUtc = DateTimeOffset.UtcNow.AddMinutes(50),
                    DurationSeconds = 3600,
                    PositionSeconds = 600,
                    RemainingSeconds = 3000,
                },
            },
            upcoming:
            [
                new DtoProgramGuideEntry
                {
                    SlotId = 11, Title = "Krimiserie — Staffel 1, E2: Der Fall",
                    PosterPictureId = 42, Description = "Episodentext.", MediaItemId = 5,
                    StartUtc = DateTimeOffset.UtcNow.AddMinutes(-10), EndUtc = DateTimeOffset.UtcNow.AddMinutes(50),
                },
                new DtoProgramGuideEntry
                {
                    SlotId = 12, Title = "Ohne-Metadaten-Film", MediaItemId = 6,
                    StartUtc = DateTimeOffset.UtcNow.AddMinutes(50), EndUtc = DateTimeOffset.UtcNow.AddMinutes(140),
                },
            ]);
        using var ctx = CreateContext(guide);

        var cut = ctx.Render<LiveTv>();

        var nowCard = cut.Find("#live-tv-now");
        Assert.Contains("now-card", nowCard.ClassList);
        Assert.Contains("compact", nowCard.ClassList);
        Assert.Single(nowCard.QuerySelectorAll(".badge-live"));
        Assert.Single(nowCard.QuerySelectorAll(".progress-fill"));
        Assert.Equal("Krimiserie", nowCard.QuerySelector(".program-title")!.TextContent.Trim());
        Assert.Contains("S1 E2", nowCard.TextContent);
        Assert.Equal($"/api/pictures/42?access_token={TestToken}",
            nowCard.QuerySelector(".program-poster img")!.GetAttribute("src"));

        var rows = cut.FindAll(".livetv-upcoming-row.program-card");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Episodentext.", rows[0].TextContent);
        Assert.Contains("/api/pictures/42", rows[0].QuerySelector(".program-poster img")!.GetAttribute("src"));

        // Ohne Bild/Beschreibung: Platzhalter-Bild + sauberer Fallback-Text.
        Assert.Contains("placeholder.png", rows[1].QuerySelector(".program-poster img")!.GetAttribute("src"));
        Assert.Contains("keine Beschreibung", rows[1].TextContent);
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
    public void Page_FeatureNotReleased_ShowsHint_WithoutGuideCallOrPoll()
    {
        var guide = CreateGuideMock();
        using var ctx = CreateContext(guide, tvSenderEnabled: false);

        var cut = ctx.Render<LiveTv>();

        Assert.Contains("nicht freigeschaltet", cut.Find("#live-tv-disabled").TextContent);
        guide.Verify(m => m.RequestProgramGuideNowAsync(It.IsAny<CancellationToken>()), Times.Never);
        guide.Verify(m => m.RequestProgramGuideUpcomingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Page_UserWithoutTvAccess_ShowsForbiddenHint_WithoutGuideCall()
    {
        var guide = CreateGuideMock();
        using var ctx = CreateContext(guide, tvSenderAccessGranted: false);

        var cut = ctx.Render<LiveTv>();

        Assert.Contains("keinen Zugriff auf den TV-Sender", cut.Find("#live-tv-no-access").TextContent);
        guide.Verify(m => m.RequestProgramGuideNowAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PollTick_RefreshesUpcomingList()
    {
        var guide = CreateGuideMock();
        using var ctx = CreateContext(guide);
        var cut = ctx.Render<LiveTv>();
        Assert.Equal(2, cut.FindAll(".livetv-upcoming-row").Count);

        // Neue Programmdaten: abgelaufener Eintrag weg, ein neuer dazu.
        guide.Setup(m => m.RequestProgramGuideUpcomingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DtoProgramGuideEntry { SlotId = 13, Title = "Neue Late-Show", MediaItemId = 7, StartUtc = DateTimeOffset.UtcNow.AddMinutes(30), EndUtc = DateTimeOffset.UtcNow.AddMinutes(90) },
            ]);
        guide.Setup(m => m.RequestProgramGuideNowAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoProgramGuideNow { ServerTimeUtc = DateTimeOffset.UtcNow });

        ControllableTime.TickTimers();
        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll(".livetv-upcoming-row")), timeout: TimeSpan.FromSeconds(5));

        Assert.Contains("Neue Late-Show", cut.Find("#live-tv-upcoming").TextContent);
        Assert.DoesNotContain("Laufender E2E-Film", cut.Find("#live-tv-upcoming").TextContent);
        guide.Verify(m => m.RequestProgramGuideUpcomingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task PollTick_NowFails_UpcomingStillRefreshes()
    {
        var guide = CreateGuideMock();
        using var ctx = CreateContext(guide);
        var cut = ctx.Render<LiveTv>();
        Assert.Equal(2, cut.FindAll(".livetv-upcoming-row").Count);

        // Der Now-Endpunkt faellt dauerhaft aus — die Vorschau darf trotzdem weiterlaufen.
        guide.Setup(m => m.RequestProgramGuideNowAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down", null, HttpStatusCode.InternalServerError));
        guide.Setup(m => m.RequestProgramGuideUpcomingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DtoProgramGuideEntry { SlotId = 14, Title = "Trotzdem-Neu", MediaItemId = 8, StartUtc = DateTimeOffset.UtcNow.AddHours(1), EndUtc = DateTimeOffset.UtcNow.AddHours(2) },
            ]);

        ControllableTime.TickTimers();
        cut.WaitForAssertion(() =>
            Assert.Contains("Trotzdem-Neu", cut.Find("#live-tv-upcoming").TextContent), timeout: TimeSpan.FromSeconds(5));
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
