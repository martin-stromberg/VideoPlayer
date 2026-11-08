using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright E2E of the on-air switch (Issue #348): the admin toggle on
/// <c>/admin/tv-sender</c> stops the live stream end to end — <c>/api/live-stream/*</c>
/// answers 404 (playlist, segments and the standby path) while the program guide keeps
/// answering — and <c>/live-tv</c> swaps the player for the off-air image without reload.
/// Toggling back resumes delivery and the player re-attaches.
/// </summary>
[Trait("Category", "E2E")]
public sealed class TvSenderOnAirE2ETests : TVSenderAdminE2ETestBase
{
    /// <summary>Seeds a channel with a running broadcast slot (movie + media item) — the
    /// program guide reports a "now" entry, so <c>/live-tv</c> renders the player on air.</summary>
    /// <param name="movieTitle">Movie name to seed and air.</param>
    private async Task SeedRunningSlotAsync(string movieTitle)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var channel = new TVChannel { Name = $"OnAir-E2E-Sender {Guid.NewGuid():N}" };
        db.TVChannels.Add(channel);
        var source = new MediaSource { Name = $"OnAir-E2E-Quelle {Guid.NewGuid():N}", Path = "/e2e-onair", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);
        var collection = new MediaCollection { Name = $"OnAir-E2E-Col {Guid.NewGuid():N}", MediaSourceId = source.Id, Path = $"/{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);
        var movie = new Movie { Name = movieTitle, MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.Movies.Add(movie);
        var item = new MediaItem { Name = "onair.mkv", Path = $"{collection.Path}/onair.mkv", MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(90), DurationProbedAt = DateTime.UtcNow };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync(ct);
        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });
        var now = DateTime.UtcNow;
        db.ProgramSlots.Add(new ProgramSlot { TVChannelId = channel.Id, Start = now.AddMinutes(-30), End = now.AddMinutes(60), MediaItemId = item.Id, MovieId = movie.Id, RuleType = ProgramRuleType.MoviePrimeTime, Status = ProgramSlotStatus.Scheduled });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Opens <c>/live-tv</c> on air and captures the playlist URL the player
    /// requests — its <c>access_token</c> authorizes the endpoint checks below.</summary>
    /// <returns>The playlist URL including the <c>access_token</c> query parameter.</returns>
    private async Task<string> CapturePlaylistUrlAsync()
    {
        var m3u8Request = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnRequest(object? _, IRequest request)
        {
            if (request.Url.Contains("/api/live-stream/live.m3u8"))
                m3u8Request.TrySetResult(request.Url);
        }
        Page.Request += OnRequest;
        try
        {
            await Page.GotoAsync($"{ServerUrl}/live-tv");
            await Page.WaitForSelectorAsync("#live-tv-video", new PageWaitForSelectorOptions { Timeout = 30_000 });
            return await m3u8Request.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
        finally
        {
            Page.Request -= OnRequest;
        }
    }

    [Fact]
    public async Task Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        await SeedRunningSlotAsync("Off-Air-Film");
        await OpenAdminPageAsAdminAsync();

        // On air: the player attaches and requests the playlist — the captured
        // access_token carries the admin's authorization for the direct checks.
        var playlistUrl = await CapturePlaylistUrlAsync();
        var onAirResponse = await Page.Context.APIRequest.GetAsync(playlistUrl);
        Assert.Equal(200, onAirResponse.Status);

        // Admin → Einstellungen → On-Air-Schalter aus (sofort wirkend, ohne „Speichern").
        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForSelectorAsync("#tvsender-tab-einstellungen", new PageWaitForSelectorOptions { Timeout = 30_000 });
        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");
        await Expect(Page.Locator("#tvsender-on-air")).ToBeCheckedAsync();
        await Page.Locator("#tvsender-on-air").ClickAsync();
        await Expect(Page.Locator("#tvsender-channel-status")).ToContainTextAsync("gestoppt",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        // Wirkung end to end: Playlist, echtes Segment und Standby-Pfad antworten 404 —
        // die Programmauskunft bleibt erreichbar (Scope-Ausschluss 3).
        Assert.Equal(404, (await Page.Context.APIRequest.GetAsync(playlistUrl)).Status);
        Assert.Equal(404, (await Page.Context.APIRequest.GetAsync(playlistUrl.Replace("live.m3u8", "standby_0_00.ts"))).Status);
        var token = playlistUrl.Split("access_token=")[1];
        Assert.Equal(200, (await Page.Context.APIRequest.GetAsync($"{ServerUrl}/api/program-guide/now?access_token={token}")).Status);

        // Zuschauer-Sicht: Off-Air-Bild statt Player, Programm-Karten laufen weiter.
        await Page.GotoAsync($"{ServerUrl}/live-tv");
        await Expect(Page.Locator("#live-tv-off-air")).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
        var naturalWidth = await Page.EvaluateAsync<int>(
            "() => document.querySelector('#live-tv-off-air')?.naturalWidth ?? 0");
        Assert.True(naturalWidth > 0, "Das Off-Air-Bild wurde nicht wirklich ausgeliefert.");
        await Expect(Page.Locator("#live-tv-now-title")).ToContainTextAsync("Off-Air-Film");
        Assert.Equal(0, await Page.Locator("#live-tv-video").CountAsync());
    }

    [Fact]
    public async Task Admin_TogglesBackOnAir_StreamAndPlayerResume()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        await SeedRunningSlotAsync("On-Air-Film");
        await SetOnAirAsync(false);
        await OpenAdminPageAsAdminAsync();

        // Off-Air-Ausgangslage: Umschalter unmarkiert, Endpunkt 404.
        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForSelectorAsync("#tvsender-tab-einstellungen", new PageWaitForSelectorOptions { Timeout = 30_000 });
        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");
        await Expect(Page.Locator("#tvsender-on-air")).Not.ToBeCheckedAsync();
        await Page.Locator("#tvsender-on-air").ClickAsync();
        await Expect(Page.Locator("#tvsender-channel-status")).ToContainTextAsync("auf Sendung",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        // Wiederaufnahme ohne Neustart: /live-tv rendert den Player wieder, der Player
        // ruft die Playlist an (echte oder Standby — jedenfalls kein 404 mehr).
        var playlistUrl = await CapturePlaylistUrlAsync();
        var response = await Page.Context.APIRequest.GetAsync(playlistUrl);
        Assert.Equal(200, response.Status);
        var playlist = await response.TextAsync();
        Assert.Contains("#EXTM3U", playlist);
    }
}
