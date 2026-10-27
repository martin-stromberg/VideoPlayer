using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Integration tests for the age-window enforcement of <see cref="ProgramScheduleService"/>:
/// unordered rings skip blocked titles while ordered strands defer, a started item runs past
/// the window edge, fillers and clip fill respect the window, the reconciliation keeps
/// reproducible airings and displaces airings that violate a tightened window, and the
/// per-rule diagnosis counts window-blocked candidates.
/// </summary>
public sealed class ProgramScheduleServiceTests_AgeWindow
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static async Task SetModeAsync(ProgramScheduleTestDb fixture, ProgramScheduleUnknownRatingMode mode, CancellationToken ct)
        => await new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance)
            .UpdateProgramScheduleUnknownRatingModeAsync(mode, ct);

    [Fact]
    public async Task EnsureCoverage_UnorderedMovieRing_SkipsBlockedAndOffersItAgainAtNight()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-flat-skip", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        await fixture.EnsureSourceAsync(1);
        var collection = new MovieCollection { Name = "Abendfilme", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        // Under the ItemOrderKey mix the blocked title sits behind the allowed one — its
        // ring position is consumed-without-airing (skipped) at every daytime occurrence.
        var blocked18 = await fixture.CreateMovieAsync("FSK18-Film", collectionId: collection.Id, releaseDate: new DateTime(2000, 1, 1), duration: TimeSpan.FromMinutes(90), ageRatingMinAge: 18);
        var allowed0 = await fixture.CreateMovieAsync("FSK0-Film", collectionId: collection.Id, releaseDate: new DateTime(2001, 1, 1), duration: TimeSpan.FromMinutes(90), ageRatingMinAge: 0);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Nachtmagazin", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.MovieCollection, collection.Id);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);

        var daySlots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.All(daySlots, s => Assert.Equal(allowed0.Id, s.MovieId));

        // The persisted cursor names the last TAKEN item — never the skipped position.
        var cursor = await fixture.Db.ProgramRuleCursors.SingleAsync(c => c.ProgramRuleId == rule.Id, ct);
        Assert.Equal(allowed0.Id, cursor.LastContentId);

        // Moving the rule into the night window re-offers the skipped ring position.
        rule.TimeOfDay = new TimeOnly(23, 30);
        await fixture.Db.SaveChangesAsync(ct);
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 25, 8, 0)), ct);

        var nightSlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == rule.Id
                && TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz).Hour == 23)
            .ToList();
        Assert.Contains(nightSlots, s => s.MovieId == blocked18.Id);
    }

    [Fact]
    public async Task EnsureCoverage_OrderedShow_BlockedHeadDefersWholeAiring()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-ordered-defer", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var (show, _, episodes) = await fixture.CreateShowAsync("Spätserie", 3, duration: TimeSpan.FromMinutes(45), ageRatingMinAge: 18);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Mittagsplatz", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);

        Assert.DoesNotContain(await fixture.SlotsAsync(ct), s => s.ProgramRuleId == rule.Id);
        // An empty take commits nothing — the strand head waits for a permissible occurrence.
        Assert.Empty(await fixture.Db.ProgramRuleCursors.Where(c => c.ProgramRuleId == rule.Id).ToListAsync(ct));

        // Inside the window the strand airs in order — the occurrence inside the 24 h
        // lookback legitimately consumed the head episode (a begun airing counts as aired).
        rule.TimeOfDay = new TimeOnly(23, 30);
        await fixture.Db.SaveChangesAsync(ct);
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 25, 8, 0)), ct);

        var nightSlots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(nightSlots);
        Assert.All(nightSlots, s => Assert.Contains(s.TVShowEpisodeId, episodes.Select(e => (long?)e.Id).ToList()));
        Assert.True(nightSlots.Any(s => s.TVShowEpisodeId == episodes[1].Id),
            $"erwartet E{episodes[1].Id}, Slots: {string.Join(", ", nightSlots.Select(s => $"ep{s.TVShowEpisodeId}@{s.Start:o}"))}");
    }

    [Fact]
    public async Task EnsureCoverage_StartedItem_RunsPastNightEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-runs-past", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var movie = await fixture.CreateMovieAsync("Nachtfilm", duration: TimeSpan.FromMinutes(90), ageRatingMinAge: 18);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Nachtslot", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(5, 30), ProgramRuleMaterialType.Movie, movie.Id);

        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 0, 0)), ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(slots);
        // 05:30 + 90 min crosses the 06:00 night end — the started item always completes.
        Assert.All(slots, s => Assert.Equal(5 * 60 + 30, LocalMinutes(s.Start)));
    }

    [Fact]
    public async Task EnsureCoverage_UnknownRating_Modes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-unknown-mode", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Freigabelos", duration: TimeSpan.FromMinutes(60));

        var rule = await fixture.CreateRuleAsync(channel.Id, "Mittagsfilm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var service = fixture.CreateService();
        // Restrictive: unrated material only airs inside the night window.
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);
        Assert.DoesNotContain(await fixture.SlotsAsync(ct), s => s.ProgramRuleId == rule.Id);

        // Unrestricted: the same material is schedulable at daytime again.
        await fixture.Db.ProgramSlots.ExecuteDeleteAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.Unrestricted, ct);
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.ProgramRuleId == rule.Id);
    }

    [Fact]
    public async Task EnsureCoverage_Interleaved_OrderedStrandDefers_OtherStrandContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-interleave-defer", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var genre = await fixture.CreateGenreAsync("Abendmix");
        var (showA, _, epA) = await fixture.CreateShowAsync("A-Serie", 4, duration: TimeSpan.FromMinutes(45), ageRatingMinAge: 18);
        var (showB, _, epB) = await fixture.CreateShowAsync("B-Serie", 4, duration: TimeSpan.FromMinutes(45), ageRatingMinAge: 0);
        await fixture.LinkShowGenreAsync(showA.Id, genre.Id);
        await fixture.LinkShowGenreAsync(showB.Id, genre.Id);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Mittagsfenster", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Genre, genre.Id,
            episodeCount: 2, genreIds: [genre.Id]);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(slots);
        // Only the child-safe strand contributes — the FSK-18 strand rests for the occurrence.
        Assert.All(slots, s => Assert.Contains(s.TVShowEpisodeId, epB.Select(e => (long?)e.Id).ToList()));

        // And its cursor only records progress for the aired strand, not the deferred one.
        var cursors = await fixture.Db.ProgramRuleCursors.Where(c => c.ProgramRuleId == rule.Id).ToListAsync(ct);
        Assert.DoesNotContain(cursors, c => epA.Select(e => e.Id).Contains(c.LastContentId));

        // Once the window opens the deferred strand picks its head up — the occurrence
        // inside the 24 h lookback legitimately consumed the head episode (a begun airing
        // counts as aired), so the first visible episode is the second ring position.
        rule.TimeOfDay = new TimeOnly(23, 30);
        await fixture.Db.SaveChangesAsync(ct);
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 25, 8, 0)), ct);

        var nightSlots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.True(nightSlots.Any(s => s.TVShowEpisodeId == epA[1].Id),
            $"erwartet E{epA[1].Id}, Slots: {string.Join(", ", nightSlots.Select(s => $"ep{s.TVShowEpisodeId}@{s.Start:o}"))}");
    }

    [Fact]
    public async Task EnsureCoverage_Interleaved_UnorderedOtherStrand_SkipsBlockedMovie()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-interleave-skip", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var genre = await fixture.CreateGenreAsync("Mischkasten");
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 4, duration: TimeSpan.FromMinutes(45), ageRatingMinAge: 0);
        await fixture.LinkShowGenreAsync(show.Id, genre.Id);
        var movie = await fixture.CreateMovieAsync("Nachtfilm", duration: TimeSpan.FromMinutes(90), ageRatingMinAge: 18);
        await fixture.LinkMovieGenreAsync(movie.Id, genre.Id);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Mittagsfenster", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Genre, genre.Id,
            episodeCount: 2, genreIds: [genre.Id]);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.All(slots, s => Assert.Contains(s.TVShowEpisodeId, episodes.Select(e => (long?)e.Id).ToList()));
    }

    [Fact]
    public async Task Reconcile_SameWindow_KeepsAiring_TighterWindow_Displaces()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-reconcile", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var movie = await fixture.CreateMovieAsync("Nachtfilm", duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 18);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Nachtslot", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(23, 30), ProgramRuleMaterialType.Movie, movie.Id);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0)), ct);
        var before = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(before);

        // Unchanged window → the airing stays (reproducible under identical semantics).
        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 13, 0)), ct);
        var kept = await fixture.Db.ProgramSlots.Where(s => s.ProgramRuleId == rule.Id && s.Status == ProgramSlotStatus.Scheduled).ToListAsync(ct);
        Assert.Equal(before.Count, kept.Count);

        // Tightening the FSK-18 start past the scheduled start displaces the airing.
        var settings = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);
        await settings.UpdateProgramScheduleAgeWindowsAsync(
            new ProgramScheduleAgeWindowSettings(new TimeOnly(20, 0), new TimeOnly(22, 0), new TimeOnly(23, 59), new TimeOnly(6, 0)), ct);

        summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 14, 0)), ct);
        Assert.True(summary.SlotsDisplaced > 0);
        var displaced = await fixture.Db.ProgramSlots.Where(s => s.ProgramRuleId == rule.Id && s.Status == ProgramSlotStatus.Displaced).ToListAsync(ct);
        Assert.NotEmpty(displaced);

        // The rematerialization stays window-conform: with the whole ring blocked at every
        // occurrence start nothing replaces the displaced airings (this is also the
        // regression scenario for the backward-hypothesis wall in FlatAiringTailReproducible —
        // a full skip lap must terminate the scan instead of looping over ~2^31 hypotheses).
        var scheduledRule = await fixture.Db.ProgramSlots
            .Where(s => s.ProgramRuleId == rule.Id && s.Status == ProgramSlotStatus.Scheduled).ToListAsync(ct);
        Assert.Empty(scheduledRule);
    }

    [Fact]
    public async Task EnsureCoverage_Filler_RespectsWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-filler", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var fillerMovie = await fixture.CreateMovieAsync("FSK18-Füller", duration: TimeSpan.FromMinutes(30), ageRatingMinAge: 18);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler,
            ProgramRuleRecurrence.Daily, new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, fillerMovie.Id, episodeCount: 1, priority: -1);

        var mainMovie = await fixture.CreateMovieAsync("Hauptfilm", duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 0);
        var mainRule = await fixture.CreateRuleAsync(channel.Id, "Mittag", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Movie, mainMovie.Id);

        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);

        // The blocked filler contributes nothing in daytime gaps.
        Assert.DoesNotContain(await fixture.SlotsAsync(ct), s => s.MovieId == fillerMovie.Id);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.ProgramRuleId == mainRule.Id);
    }

    [Fact]
    public async Task EnsureCoverage_ClipFill_RespectsWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-clipfill", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        // SlotDuration rule with a short movie leaves a remainder for clip fill; the clip
        // carries no rating — under the restrictive mode it may not air at daytime.
        var movie = await fixture.CreateMovieAsync("Kurzfilm", duration: TimeSpan.FromMinutes(20), ageRatingMinAge: 0);
        var clipGroup = await fixture.CreateClipGroupAsync("Zwischenclips", ct);
        var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(5));
        await fixture.AddClipToGroupAsync(clipGroup.Id, clip.Id, ct);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Werbeblock", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromMinutes(30), clipGroupIds: [clipGroup.Id]);

        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.Contains(slots, s => s.MovieId == movie.Id);
        Assert.DoesNotContain(slots, s => s.IsClipFill && s.MediaItemId == clip.Id);
    }

    [Fact]
    public async Task Diagnosis_CountsWindowBlockedAtRuleEvaluationTime()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-diagnosis", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        // Rule at 12:00 daily → its evaluation start is inside the restricted daytime.
        var dayMovie = await fixture.CreateMovieAsync("Tagfilm", duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 0);
        var nightMovie = await fixture.CreateMovieAsync("FSK18-Film", duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 18);
        var collection = new MovieCollection { Name = "Mix", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        dayMovie.MovieCollectionId = collection.Id;
        nightMovie.MovieCollectionId = collection.Id;
        await fixture.Db.SaveChangesAsync(ct);

        var dayRule = await fixture.CreateRuleAsync(channel.Id, "Mittagsregel", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.MovieCollection, collection.Id);
        var nightRule = await fixture.CreateRuleAsync(channel.Id, "Nachtregel", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(23, 30), ProgramRuleMaterialType.MovieCollection, collection.Id);

        var diagnoses = await fixture.CreateService().GetRuleDiagnosesAsync(admin.Id, channel.Id, ct);

        Assert.Equal(1, diagnoses[dayRule.Id].AgeWindowBlockedCount);
        Assert.Equal(0, diagnoses[nightRule.Id].AgeWindowBlockedCount);
    }

    [Fact]
    public async Task EnsureCoverage_MixedRatings_Fsk16AirsOnlyFrom22()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-fsk16-boundary", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var collection = new MovieCollection { Name = "Spielfilme", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        // At 21:30 the FSK-12 window is already open but the FSK-16 one is not — under the
        // ItemOrderKey mix the FSK-16 title sits behind the FSK-0 head and its ring position
        // is consumed-without-airing (skipped) at the 21:30 occurrences.
        var fsk16 = await fixture.CreateMovieAsync("FSK16-Film", collectionId: collection.Id, releaseDate: new DateTime(2000, 1, 1), duration: TimeSpan.FromMinutes(90), ageRatingMinAge: 16);
        var fsk0 = await fixture.CreateMovieAsync("FSK0-Film", collectionId: collection.Id, releaseDate: new DateTime(2001, 1, 1), duration: TimeSpan.FromMinutes(90), ageRatingMinAge: 0);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(21, 30), ProgramRuleMaterialType.MovieCollection, collection.Id);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0)), ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(fsk0.Id, s.MovieId));

        // From 22:00 the FSK-16 window is open — the previously skipped position airs.
        rule.TimeOfDay = new TimeOnly(22, 30);
        await fixture.Db.SaveChangesAsync(ct);
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 25, 12, 0)), ct);

        var nightSlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == rule.Id
                && TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz).Hour == 22)
            .ToList();
        Assert.Contains(nightSlots, s => s.MovieId == fsk16.Id);
    }

    [Theory]
    [InlineData("R", 17, 21, 30, 22, 30)]     // R → 17 ≙ FSK-16 window from 22:00
    [InlineData("PG-13", 13, 19, 30, 20, 30)] // PG-13 → 13 ≙ FSK-12 window from 20:00
    [InlineData("NC-17", 18, 22, 30, 23, 30)] // NC-17 → 18 ≙ FSK-18 window from 23:00
    public async Task EnsureCoverage_MpaaRatedMovie_RespectsMappedWindow(
        string rating, int expectedMinAge, int blockedHour, int blockedMinute, int allowedHour, int allowedMinute)
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync($"agewindow-mpaa-{rating}", ct);
        var channel = await fixture.GetChannelAsync(ct);

        // The raw rating runs through the same normalization the NFO import uses.
        var movie = await fixture.CreateMovieAsync("Spielfilm", duration: TimeSpan.FromMinutes(60), ageRating: rating);
        Assert.Equal(expectedMinAge, movie.AgeRatingMinAge);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Sendeplatz", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(blockedHour, blockedMinute), ProgramRuleMaterialType.Movie, movie.Id);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0)), ct);
        Assert.DoesNotContain(await fixture.SlotsAsync(ct), s => s.ProgramRuleId == rule.Id);

        rule.TimeOfDay = new TimeOnly(allowedHour, allowedMinute);
        await fixture.Db.SaveChangesAsync(ct);
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 25, 12, 0)), ct);
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.ProgramRuleId == rule.Id && s.MovieId == movie.Id);
    }

    [Fact]
    public async Task Reconcile_InterleavedWindowSkip_StaysReproducible_TighterWindowDisplaces()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-interleave-repro", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        // Interleaved genre rule: an ordered show strand (FSK 12) plus an unordered "other"
        // movie ring whose middle position under the ItemOrderKey mix is FSK-18-blocked
        // during the evening window — the airing takes the ring positions 0 and 2
        // (a provable skip gap).
        var genre = await fixture.CreateGenreAsync("Abendmix");
        var (show, _, _) = await fixture.CreateShowAsync("Serie", 6, duration: TimeSpan.FromMinutes(45), ageRatingMinAge: 12);
        await fixture.LinkShowGenreAsync(show.Id, genre.Id);
        var m1 = await fixture.CreateMovieAsync("Familienfilm", releaseDate: new DateTime(2000, 1, 1), duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 0);
        var m2 = await fixture.CreateMovieAsync("Nachtfilm", releaseDate: new DateTime(2001, 1, 1), duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 18);
        var m3 = await fixture.CreateMovieAsync("Nachmittagsfilm", releaseDate: new DateTime(2002, 1, 1), duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 0);
        await fixture.LinkMovieGenreAsync(m1.Id, genre.Id);
        await fixture.LinkMovieGenreAsync(m2.Id, genre.Id);
        await fixture.LinkMovieGenreAsync(m3.Id, genre.Id);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 30), ProgramRuleMaterialType.Genre, genre.Id,
            episodeCount: 4, genreIds: [genre.Id]);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 19, 0)), ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        // The airing interleaves show episodes and "other" movies; the FSK-18 position was
        // skipped inside the window (taken ring positions are non-consecutive).
        Assert.Contains(slots, s => s.TVShowEpisodeId is not null);
        Assert.Contains(slots, s => s.MovieId is not null);
        Assert.DoesNotContain(slots, s => s.MovieId == m2.Id);

        // Unchanged settings: the gap-tolerant reproducibility check keeps the airing.
        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 19, 30)), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);

        // Tightening the FSK-12 window past the occurrence start displaces the airing; the
        // rematerialization defers the ordered strand and fills with allowed movies only.
        var settings = new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance);
        await settings.UpdateProgramScheduleAgeWindowsAsync(
            new ProgramScheduleAgeWindowSettings(new TimeOnly(21, 30), new TimeOnly(22, 0), new TimeOnly(23, 0), new TimeOnly(6, 0)), ct);

        summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 19, 45)), ct);
        Assert.True(summary.SlotsDisplaced > 0);

        var rematerialized = await fixture.Db.ProgramSlots
            .Where(s => s.ProgramRuleId == rule.Id && s.Status == ProgramSlotStatus.Scheduled).ToListAsync(ct);
        Assert.NotEmpty(rematerialized);
        Assert.All(rematerialized, s => Assert.Null(s.TVShowEpisodeId));
        foreach (var slot in rematerialized.Where(s => s.MovieId == m2.Id))
            Assert.True(LocalMinutes(slot.Start) >= 23 * 60 || LocalMinutes(slot.Start) < 6 * 60,
                $"FSK-18-Slot {slot.Start:o} außerhalb des Nachtfensters");
    }

    [Fact]
    public async Task Reconcile_DisplacedUnorderedRing_ReoffersSkippedPositionOnlyInWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-rewind-reoffer", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        // Unordered ring under the ItemOrderKey mix [FSK 18, FSK 0, FSK 0]: the blocked
        // position is consumed-without-airing (scanned and skipped) at every daytime
        // occurrence — regardless of which position it lands on.
        var collection = new MovieCollection { Name = "Rotation", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        var a = await fixture.CreateMovieAsync("Film A", collectionId: collection.Id, releaseDate: new DateTime(2000, 1, 1), duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 0);
        var b = await fixture.CreateMovieAsync("Film B", collectionId: collection.Id, releaseDate: new DateTime(2001, 1, 1), duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 18);
        var c = await fixture.CreateMovieAsync("Film C", collectionId: collection.Id, releaseDate: new DateTime(2002, 1, 1), duration: TimeSpan.FromMinutes(60), ageRatingMinAge: 0);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Mittagsplatz", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.MovieCollection, collection.Id, episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);

        // Deactivating displaces the whole future of the rule and rewinds the cursor onto
        // the earliest displaced content — the skipped position behind it is re-offered.
        rule.IsActive = false;
        await fixture.Db.SaveChangesAsync(ct);
        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 11, 0)), ct);
        Assert.True(summary.SlotsDisplaced > 0);

        rule.IsActive = true;
        await fixture.Db.SaveChangesAsync(ct);
        await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 11, 30)), ct);

        // The displaced titles re-air in ring order; the skipped position is re-evaluated
        // and stays unaired while its window is closed.
        var daySlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == rule.Id && s.Status == ProgramSlotStatus.Scheduled
                && LocalMinutes(s.Start) >= 6 * 60 && LocalMinutes(s.Start) < 20 * 60)
            .ToList();
        Assert.Contains(daySlots, s => s.MovieId == a.Id);
        Assert.Contains(daySlots, s => s.MovieId == c.Id);
        Assert.DoesNotContain(daySlots, s => s.MovieId == b.Id);

        // Inside its window the skipped position really airs — the same re-evaluation.
        rule.TimeOfDay = new TimeOnly(23, 30);
        await fixture.Db.SaveChangesAsync(ct);
        await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 14, 0)), ct);

        var bSlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.MovieId == b.Id && s.Status == ProgramSlotStatus.Scheduled).ToList();
        Assert.NotEmpty(bSlots);
        Assert.All(bSlots, s => Assert.True(LocalMinutes(s.Start) >= 23 * 60 || LocalMinutes(s.Start) < 6 * 60));
    }

    [Fact]
    public async Task EnsureCoverage_WeeklyRule_StartedItemRunsPastNightEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-weekly-edge", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        // 2026-10-24 is a Saturday — the weekly occurrence starts inside the window and the
        // begun title runs past the 06:00 night end ("a started item always completes").
        var movie = await fixture.CreateMovieAsync("Nachtfilm", duration: TimeSpan.FromMinutes(90), ageRatingMinAge: 18);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Wochenendslot", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Weekly, new TimeOnly(5, 30), ProgramRuleMaterialType.Movie, movie.Id,
            dayOfWeek: DayOfWeek.Saturday);

        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 0, 0)), ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(5 * 60 + 30, LocalMinutes(s.Start)));
    }

    [Fact]
    public async Task EnsureCoverage_WeeklyRule_At0630_Fsk18Blocked()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("agewindow-weekly-blocked", ct);
        var channel = await fixture.GetChannelAsync(ct);
        await SetModeAsync(fixture, ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        // Same weekly slot half an hour later: 06:30 lies outside the night window —
        // the FSK-18 title may not start.
        var movie = await fixture.CreateMovieAsync("Nachtfilm", duration: TimeSpan.FromMinutes(90), ageRatingMinAge: 18);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Morgenslot", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Weekly, new TimeOnly(6, 30), ProgramRuleMaterialType.Movie, movie.Id,
            dayOfWeek: DayOfWeek.Saturday);

        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 0, 0)), ct);

        Assert.DoesNotContain(await fixture.SlotsAsync(ct), s => s.ProgramRuleId == rule.Id);
    }

    private static int LocalMinutes(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tz).TimeOfDay.Hours * 60
            + TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tz).TimeOfDay.Minutes;
}
