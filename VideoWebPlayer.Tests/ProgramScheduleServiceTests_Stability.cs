using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Stabilitäts-Regressionen für Issue #310: realistische Sendeplan-Szenarien, die über
/// Reconcile-Läufe hinweg kein einziges Displaced erzeugen dürfen (gemischter Tagesplan,
/// Playlist-Repräsentantenwechsel, Clip-Füllung).
/// </summary>
public sealed class ProgramScheduleServiceTests_Stability
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private readonly ITestOutputHelper _output;

    public ProgramScheduleServiceTests_Stability(ITestOutputHelper output) => _output = output;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static async Task<List<ProgramSlot>> ScheduledAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();

    // Typisches Senderbild: Serie am Nachmittag, Spielfilm abends, Genre-Rotation vormittags,
    // Füller — mehrere Reconcile-Takte zu unterschiedlichen Zeiten, inkl. mitten in Ausstrahlungen.
    [Fact]
    public async Task Repro_MixedSchedule_ReconcileLoops_NoFalsePositives()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("repro-mixed", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, _) = await fixture.CreateShowAsync("Nachmittagsserie", 40, duration: TimeSpan.FromMinutes(45));
        var (showA, _, _) = await fixture.CreateShowAsync("SerieA", 30, duration: TimeSpan.FromMinutes(50));
        var (showB, _, _) = await fixture.CreateShowAsync("SerieB", 30, duration: TimeSpan.FromMinutes(47));
        var genre = await fixture.CreateGenreAsync("Drama");
        await fixture.LinkShowGenreAsync(showA.Id, genre.Id);
        await fixture.LinkShowGenreAsync(showB.Id, genre.Id);
        var movie = await fixture.CreateMovieAsync("Primetime-Film", duration: TimeSpan.FromMinutes(110));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        await fixture.CreateRuleAsync(channel.Id, "Nachmittag", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(14, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 4);
        await fixture.CreateRuleAsync(channel.Id, "Vormittag", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(9, 0), ProgramRuleMaterialType.Genre, genre.Id, episodeCount: 3);
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 15), ProgramRuleMaterialType.Movie, movie.Id, episodeCount: 1);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var messages = new ConcurrentQueue<string>();
        var logger = new ListLogger<ProgramScheduleService>(messages);
        var service = fixture.CreateService(logger: logger);

        var build = new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0));
        await service.EnsureCoverageAsync(build, ct);

        var times = new[]
        {
            LocalUtc(2026, 10, 24, 10, 15),   // mitten in Vormittags-Interleaving
            LocalUtc(2026, 10, 24, 15, 40),   // mitten in Nachmittagsserie
            LocalUtc(2026, 10, 24, 21, 0),    // mitten im Spielfilm
            LocalUtc(2026, 10, 25, 9, 30),
            LocalUtc(2026, 10, 26, 2, 0),
        };
        foreach (var t in times)
        {
            var summary = await service.ReconcileAsync(new DateTimeOffset(t), ct);
            _output.WriteLine($"{t:o}: displaced={summary.SlotsDisplaced} created={summary.SlotsCreated}");
            await service.EnsureCoverageAsync(new DateTimeOffset(t), ct);
        }

        foreach (var m in messages.Where(m => m.Contains("invalide")))
            _output.WriteLine("LOG: " + m);

        var displacedCount = (await fixture.SlotsAsync(ct)).Count(s => s.Status == ProgramSlotStatus.Displaced);
        _output.WriteLine($"Displaced gesamt: {displacedCount}");
        Assert.Equal(0, displacedCount);
    }

    // Playlist-Pendant des Issue-#290-Repräsentantenwechsels: die geschriebene Ausstrahlung
    // trägt die zweite (höher-id) Datei, weil die erste beim Planungslauf unprobed war. Wird
    // sie später probed, kippt der Repräsentant — der Inhalts-Fallback muss den
    // Playlist-Eintrag (ContentType PlaylistEntry, nicht Movie) trotzdem wiederfinden.
    [Fact]
    public async Task Reconcile_PlaylistSecondMediaItemProbed_SlotStaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("repro-playlist-flip", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var user = await fixture.AddUserAsync("owner@test.local");
        await fixture.GrantSourceAccessAsync(user.Id);
        var movieA = await fixture.CreateMovieAsync("FilmA", duration: TimeSpan.FromMinutes(90));
        var movieB = await fixture.CreateMovieAsync("FilmB", duration: TimeSpan.FromMinutes(45));

        var playlist = new Playlist { UserId = user.Id, Name = "Abendliste", SortMode = PlaylistSortMode.Manual };
        fixture.Db.Playlists.Add(playlist);
        await fixture.SaveDiagAsync();
        var sort = 0;
        foreach (var movieId in new[] { movieA.Id, movieB.Id })
        {
            fixture.Db.PlaylistEntries.Add(new PlaylistEntry
            {
                PlaylistId = playlist.Id,
                MediaType = MediaTypeValues.Movie,
                MediaId = movieId,
                SortOrder = sort++,
                AddedAt = DateTime.UtcNow
            });
        }
        await fixture.SaveDiagAsync();

        // Die erste Datei von FilmA ist unprobed — die zweite trägt den Sendeplatz.
        var seededItemId = await fixture.Db.MovieMediaItems.Where(l => l.MovieId == movieA.Id).Select(l => l.MediaItemId).SingleAsync(ct);
        var secondItem = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieMediaItemAsync(movieA.Id, secondItem.Id);
        await fixture.Db.MediaItems.Where(m => m.Id == seededItemId)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Duration, (TimeSpan?)null), ct);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Abendliste", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Playlist, playlist.Id, episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var movieSlots = (await ScheduledAsync(fixture, ct)).Where(s => s.MovieId == movieA.Id).ToList();
        Assert.NotEmpty(movieSlots);
        Assert.All(movieSlots, s => Assert.Equal(secondItem.Id, s.MediaItemId));

        // Die Probe der ersten Datei holt nach — der Repräsentant kippt zurück.
        await fixture.Db.MediaItems.Where(m => m.Id == seededItemId)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Duration, (TimeSpan?)TimeSpan.FromMinutes(120)), ct);

        var summary = await service.ReconcileAsync(now, ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);
        var after = (await ScheduledAsync(fixture, ct)).Where(s => s.MovieId == movieA.Id).ToList();
        Assert.Equal(movieSlots.Select(s => (s.Start, s.End, s.MediaItemId)).ToList(),
            after.Select(s => (s.Start, s.End, s.MediaItemId)).ToList());
        var second = await service.ReconcileAsync(now, ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, second, ct, "2. Lauf");
    }

    // Clip-Füllung: ein Titel-Fenster mit Clip-Gruppe — Ausstrahlung mit Clip-Anteilen.
    [Fact]
    public async Task Repro_ClipFillAiring_ReconcileLoops_NoFalsePositives()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("repro-clipfill", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, _) = await fixture.CreateShowAsync("Serie", 40, duration: TimeSpan.FromMinutes(45));
        var clipGroup = await fixture.CreateClipGroupAsync("Werbung", ct);
        for (var i = 0; i < 5; i++)
        {
            var clip = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(3));
            await fixture.AddClipToGroupAsync(clipGroup.Id, clip.Id, ct);
        }

        await fixture.CreateRuleAsync(channel.Id, "Nachmittag", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(14, 0), ProgramRuleMaterialType.TVShow, show.Id,
            slotDuration: TimeSpan.FromHours(4), clipGroupIds: new[] { clipGroup.Id },
            placeholderGrace: TimeSpan.FromMinutes(10));

        var messages = new ConcurrentQueue<string>();
        var logger = new ListLogger<ProgramScheduleService>(messages);
        var service = fixture.CreateService(logger: logger);

        var build = new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0));
        await service.EnsureCoverageAsync(build, ct);

        foreach (var t in new[] { LocalUtc(2026, 10, 24, 15, 40), LocalUtc(2026, 10, 25, 10, 0) })
        {
            var summary = await service.ReconcileAsync(new DateTimeOffset(t), ct);
            _output.WriteLine($"{t:o}: displaced={summary.SlotsDisplaced} created={summary.SlotsCreated}");
        }
        foreach (var m in messages.Where(m => m.Contains("invalide")))
            _output.WriteLine("LOG: " + m);
        var displacedCount = (await fixture.SlotsAsync(ct)).Count(s => s.Status == ProgramSlotStatus.Displaced);
        _output.WriteLine($"Displaced gesamt: {displacedCount}");
    }
}
