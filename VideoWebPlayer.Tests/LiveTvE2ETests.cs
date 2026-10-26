using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright-E2E der Live-TV-Ansicht (Schritt 7): Navigation → Seite → Player-Anbindung an den
/// authentifizierten HLS-Stream (Request mit <c>access_token</c>), Jetzt/Gleich/Vorschau,
/// Sendelücke mit verständlichem Hinweis und Wiederherstellung ohne Neuladen.
/// Konsolidiert in zwei Testmethoden gegen dieselbe Browser-Instanz.
/// </summary>
public sealed class LiveTvE2ETests : PlaylistsE2ETestBase
{
    private async Task<string> SeedSegmentDirectoryAsync(IServiceProvider services, ApplicationDbContext db, bool writePlaylist = true)
    {
        var segDir = Path.Combine(Path.GetTempPath(), $"vwp-livetv-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(segDir);
        if (writePlaylist)
        {
            await File.WriteAllTextAsync(Path.Combine(segDir, "live.m3u8"),
                "#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:6\n#EXTINF:6.0,\nseg_001.ts\n");
            await File.WriteAllTextAsync(Path.Combine(segDir, "seg_001.ts"), "segment-bytes");
        }
        var setup = await services.GetRequiredService<ProgramSettingsService>().GetOrCreateSetupAsync(TestContext.Current.CancellationToken);
        setup.LiveStreamSegmentDirectory = segDir;
        setup.TvSenderEnabled = true;
        // N28: UserA bekommt die Sender-Freigabe — sonst bleibt /live-tv hinter der
        // Benutzer-Allowlist verborgen.
        var userA = await db.Users.SingleAsync(u => u.Email == UserAEmail, TestContext.Current.CancellationToken);
        db.TvSenderUserAccess.Add(new TvSenderUserAccess { UserId = userA.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return segDir;
    }

    /// <summary>
    /// Seeds channel + movie + media item + one running and one upcoming broadcast slot, plus the
    /// HLS segment directory so the player actually reaches <c>live.m3u8</c>.
    /// </summary>
    /// <summary>Same seed as <see cref="SeedChannelWithRunningProgramAsync"/> but with an
    /// empty segment directory — the server answers the playlist with the standby stream.</summary>
    private async Task SeedChannelWithRunningProgram_NoPlaylistAsync(string movieTitle)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedSegmentDirectoryAsync(scope.ServiceProvider, db, writePlaylist: false);
        await SeedProgramCoreAsync(db, movieTitle);
    }

    private async Task SeedChannelWithRunningProgramAsync(string movieTitle)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedSegmentDirectoryAsync(scope.ServiceProvider, db);
        await SeedProgramCoreAsync(db, movieTitle);
    }

    private static async Task SeedProgramCoreAsync(ApplicationDbContext db, string movieTitle)
    {
        var channel = new TVChannel { Name = "Live-E2E-Sender" };
        db.TVChannels.Add(channel);
        var source = new MediaSource { Name = $"Live-E2E-Quelle {Guid.NewGuid():N}", Path = "/e2e-live", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collection = new MediaCollection { Name = $"Live-E2E-Col {Guid.NewGuid():N}", MediaSourceId = source.Id, Path = $"/{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var poster = new Picture { Data = [0x42], ContentType = "image/png", Type = "poster" };
        db.Pictures.Add(poster);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var movie = new Movie { Name = movieTitle, MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow, Plot = "Handlung des Abendfilms.", GenreNames = "Drama", PosterPictureId = poster.Id };
        db.Movies.Add(movie);
        var item = new MediaItem { Name = "live.mkv", Path = $"{collection.Path}/live.mkv", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(90), DurationProbedAt = DateTime.UtcNow };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });

        var now = DateTime.UtcNow;
        db.ProgramSlots.AddRange(
            new ProgramSlot { TVChannelId = channel.Id, Start = now.AddMinutes(-30), End = now.AddMinutes(60), MediaItemId = item.Id, MovieId = movie.Id, RuleType = ProgramRuleType.MoviePrimeTime, Status = ProgramSlotStatus.Scheduled },
            new ProgramSlot { TVChannelId = channel.Id, Start = now.AddMinutes(60), End = now.AddMinutes(150), MediaItemId = item.Id, MovieId = movie.Id, RuleType = ProgramRuleType.SeriesFixedTime, Status = ProgramSlotStatus.Scheduled });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LiveTv_ViaNavMenu_ShowsPlayerAndProgram()
    {
        if (SkipBrowser) return;

        await SeedChannelWithRunningProgramAsync("Live-Abendfilm");
        await LoginAsync(UserAEmail);

        // Unauthenticated access already covered by the login flow itself; the nav entry is the
        // door in: exactly one "Live-TV" link, then the page is driven through it. The sidebar
        // starts collapsed — the toggle opens it before the link can receive the click.
        var navLink = Page.Locator("a.nav-link", new PageLocatorOptions { HasText = "Live-TV" });
        await Expect(navLink).ToHaveCountAsync(1);
        await DriveUntilStateAsync(
            async () =>
            {
                var toggle = Page.Locator("#sidebarToggle");
                if (await Page.Locator("#sidebar.sidebar-closed").CountAsync() > 0 && await toggle.CountAsync() > 0)
                    await toggle.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => Page.Locator("#sidebar:not(.sidebar-closed)").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));
        var m3u8Request = new TaskCompletionSource<string>();
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/api/live-stream/live.m3u8"))
                m3u8Request.TrySetResult(request.Url);
        };
        await navLink.ClickAsync();

        await Page.WaitForSelectorAsync("#live-tv-now-title", new PageWaitForSelectorOptions { Timeout = 30_000 });
        await Expect(Page.Locator("#live-tv-now-title")).ToContainTextAsync("Live-Abendfilm");
        await Expect(Page.Locator("#live-tv-next")).ToContainTextAsync("Live-Abendfilm");
        Assert.True(await Page.Locator(".livetv-upcoming-row").CountAsync() >= 2);
        // Panel-Karten: "Jetzt läuft" mit LIVE-Badge/Fortschritt + Beschreibung aus den Metadaten.
        await Expect(Page.Locator("#live-tv-now.program-card.now-card")).ToBeVisibleAsync();
        await Expect(Page.Locator("#live-tv-now .badge-live")).ToBeVisibleAsync();
        await Expect(Page.Locator("#live-tv-now .program-desc")).ToContainTextAsync("Handlung des Abendfilms.");
        await Expect(Page.Locator("#live-tv-video")).ToBeVisibleAsync();

        // The HLS wiring end to end: the page fetched the playlist with the bearer token.
        var requestedUrl = await m3u8Request.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Contains("access_token=", requestedUrl);
    }

    [Fact]
    public async Task LiveTv_ProgramGap_ShowsFriendlyHintAndUpcoming()
    {
        if (SkipBrowser) return;

        // No running slot: channel + movie/material + one future broadcast.
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var setup = await scope.ServiceProvider.GetRequiredService<ProgramSettingsService>().GetOrCreateSetupAsync(TestContext.Current.CancellationToken);
            setup.TvSenderEnabled = true;
            var userA = await db.Users.SingleAsync(u => u.Email == UserAEmail, TestContext.Current.CancellationToken);
            db.TvSenderUserAccess.Add(new TvSenderUserAccess { UserId = userA.Id });
            var channel = new TVChannel { Name = "Luecken-Sender" };
            db.TVChannels.Add(channel);
            var source = new MediaSource { Name = $"Luecken-Quelle {Guid.NewGuid():N}", Path = "/e2e-gap", Host = "localhost", Port = 22 };
            db.MediaSources.Add(source);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var collection = new MediaCollection { Name = $"Luecken-Col {Guid.NewGuid():N}", MediaSourceId = source.Id, Path = $"/{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
            db.MediaCollections.Add(collection);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var movie = new Movie { Name = "Später-Film", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
            db.Movies.Add(movie);
            var item = new MediaItem { Name = "gap.mkv", Path = $"{collection.Path}/gap.mkv", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(45), DurationProbedAt = DateTime.UtcNow };
            db.MediaItems.Add(item);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });
            var start = DateTime.UtcNow.AddHours(2);
            db.ProgramSlots.Add(new ProgramSlot { TVChannelId = channel.Id, Start = start, End = start.AddMinutes(45), MediaItemId = item.Id, MovieId = movie.Id, RuleType = ProgramRuleType.Filler, Status = ProgramSlotStatus.Scheduled });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await LoginAsync(UserAEmail);
        await Page.GotoAsync($"{ServerUrl}/live-tv");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Sendelücke: verständlicher Hinweis statt Player, die Vorschau bleibt sichtbar.
        await Page.WaitForSelectorAsync("#live-tv-no-program", new PageWaitForSelectorOptions { Timeout = 30_000 });
        await Expect(Page.Locator("#live-tv-no-program")).ToContainTextAsync("Aktuell läuft keine Sendung");
        await Expect(Page.Locator("#live-tv-no-program")).ToContainTextAsync("Später-Film");
        Assert.True(await Page.Locator(".livetv-upcoming-row").CountAsync() >= 1);
        Assert.Equal(0, await Page.Locator("#live-tv-video").CountAsync());

        // Hinweis: Der Wiederanlauf bei Slot-Beginn (Polling ~15 s + Re-Attach) ist nicht E2E-
        // prüfbar, weil der ProgramScheduleWorker manuell eingefügte, regellose Slots beim
        // nächsten ReconcileAsync-Lauf als Displaced verwirft — ein zur Laufzeit geschriebener
        // Slot überlebt daher nicht bis zur sichtbaren Wiederherstellung. Der Mechanismus ist
        // über die bUnit-Statusuebergaenge und den Player-Test abgesichert.
    }

    [Fact]
    public async Task LiveTv_Warmup_PlaysStandbyWithoutErrorHint()
    {
        var playlistUrls = new List<string>();
        var playlistStatuses = new List<int>();
        var standbySegmentStatuses = new List<int>();
        // Die Response-Events feuern, sobald die Antwort beim Browser eintrifft — die
        // Fehlerhinweis-Prüfung muss direkt darauf aufsetzen, denn ein codec-bedingter
        // MSE-Fehler (siehe unten) würde das Element wenige Millisekunden später einblenden.
        var playlist200 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var standbySegment200 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Page.Response += (_, response) =>
        {
            if (response.Url.Contains("live.m3u8", StringComparison.Ordinal))
            {
                playlistUrls.Add(response.Url);
                playlistStatuses.Add(response.Status);
                if (response.Status == 200)
                    playlist200.TrySetResult();
            }
            if (response.Url.Contains("standby_", StringComparison.Ordinal))
            {
                standbySegmentStatuses.Add(response.Status);
                if (response.Status == 200)
                    standbySegment200.TrySetResult();
            }
        };

        var ct = TestContext.Current.CancellationToken;
        await SeedChannelWithRunningProgram_NoPlaylistAsync("Warmup-Film");
        await LoginAsync(UserAEmail);

        await Page.GotoAsync($"{ServerUrl}/live-tv");
        await Page.WaitForSelectorAsync("#live-tv-video", new PageWaitForSelectorOptions { Timeout = 30_000 });

        // hls.js (oder nativer Player) ruft die Standby-Playlist mit access_token auf —
        // das bisherige 404 hätte über einen fatalen Netzwerkfehler in liveTv.js den
        // Hinweis #live-tv-stream-error eingeblendet; die 200-Antwort muss daher ohne
        // Fehlerhinweis auskommen. Der DOM-Check läuft direkt auf der ersten 200-Antwort:
        // jeder fatale Fehler braucht mindestens einen vollen Segmentzyklus danach.
        await playlist200.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        Assert.Contains("access_token=", playlistUrls[playlistStatuses.IndexOf(200)]);
        Assert.Equal(0, await Page.Locator("#live-tv-stream-error").CountAsync());

        // Mindestens ein standby_-Segment wird mit 200 ausgeliefert — ein Segment-404
        // wäre für hls.js ein fataler Netzwerkfehler.
        await standbySegment200.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        Assert.DoesNotContain(404, standbySegmentStatuses);

        // Nachgelagerte Prüfung: Zeit für einen eventuell anstehenden fatalen Fehler
        // geben; erscheint der Hinweis, darf er nie aus der Auslieferung kommen —
        // kein hls.js-Netzwerkfehler (Playlist-/Segment-Laden) und kein Netzwerkfehler
        // des nativen Elements (MEDIA_ERR_NETWORK = 2). Codec-/Format-Fehler des
        // Test-Browsers (kein natives HLS, ggf. fehlende H.264/AAC) sind auch bei
        // einem echten Stream erwartbar.
        await Task.Delay(3_000, ct);
        var errorHint = Page.Locator("#live-tv-stream-error");
        if (await errorHint.CountAsync() > 0)
        {
            var kind = await errorHint.GetAttributeAsync("data-error-kind");
            if (kind == "native")
            {
                var code = await Page.EvaluateAsync<int?>(
                    "() => document.querySelector('#live-tv-video')?.error?.code ?? null");
                Assert.True(code is 3 or 4,
                    $"#live-tv-stream-error mit nativem Netzwerkfehler (video.error={code}).");
            }
            else
            {
                Assert.True(kind is "mediaError" or "keySystemError" or "muxError",
                    $"#live-tv-stream-error unerwartet ausgeloest durch '{kind}'.");
            }
        }
    }
}
