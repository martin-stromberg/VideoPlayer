using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared seed and assertion helpers for the primetime scenario tests of issue.md
/// ("Senderplanung fehlerhaft"): a daily 20:15 genre-rotation rule with a 225-minute
/// slot duration, a 15-minute placeholder grace, user clip groups plus the reserved
/// trailer group, and a 7-day planning horizon. Local-time helpers mirror the
/// production basis (<see cref="TimeZoneInfo.Local"/>) used by occurrence building
/// and the age window.
/// </summary>
internal static class PrimetimeSeed
{
    /// <summary>Daily primetime occurrence start (local time).</summary>
    /// <value>20:15 local.</value>
    public static readonly TimeOnly Start = new(20, 15);

    /// <summary>Nominal period length of one airing (20:15 → 00:00 local).</summary>
    /// <value>225 minutes.</value>
    public static readonly TimeSpan Period = TimeSpan.FromMinutes(225);

    /// <summary>Placeholder grace of the rule: a title may still start while at least
    /// this much room remains before the period end.</summary>
    /// <value>15 minutes.</value>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(15);

    /// <summary>Planning horizon of the scenario: 7 days.</summary>
    public const int HorizonDays = 7;

    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;
    private static readonly TimeSpan PrimetimeStartOfDay = Start.ToTimeSpan();

    /// <summary>UTC instant of a local wall-clock time (same conversion the
    /// occurrence builder uses).</summary>
    /// <param name="year">The local year.</param>
    /// <param name="month">The local month.</param>
    /// <param name="day">The local day of the month.</param>
    /// <param name="hour">The local hour.</param>
    /// <param name="minute">The local minute.</param>
    /// <returns>The UTC instant of the given local wall-clock time.</returns>
    public static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    /// <summary>Local wall-clock reading of a stored UTC slot time.</summary>
    /// <param name="utc">The stored UTC slot time.</param>
    /// <returns>The local wall-clock reading of <paramref name="utc"/>.</returns>
    public static DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tz);

    /// <summary>Minutes after local midnight of a stored UTC slot time.</summary>
    /// <param name="utc">The stored UTC slot time.</param>
    /// <returns>The minutes after local midnight.</returns>
    public static double LocalMinutesOfDay(DateTime utc)
        => ToLocal(utc).TimeOfDay.TotalMinutes;

    /// <summary>The evening an airing belongs to: slots starting between 20:15 and the
    /// next day's 20:15 count toward the former evening (overshoot past midnight stays
    /// part of the day it started on).</summary>
    /// <param name="utc">The stored UTC start of the slot.</param>
    /// <returns>The local evening date the slot belongs to.</returns>
    public static DateOnly EveningDateOf(DateTime utc)
    {
        var local = ToLocal(utc);
        return DateOnly.FromDateTime(local.TimeOfDay >= PrimetimeStartOfDay ? local : local.AddDays(-1));
    }

    /// <summary>Sets the planning horizon in whole days.</summary>
    /// <param name="fixture">The seeded test database.</param>
    /// <param name="days">The planning horizon in days.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SetHorizonDaysAsync(ProgramScheduleTestDb fixture, int days, CancellationToken ct)
    {
        var setup = await fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleHorizonHours = days * 24;
        await fixture.Db.SaveChangesAsync(ct);
    }

    /// <summary>Creates the scenario rule: genre rotation, daily at 20:15 local,
    /// 225-minute slot duration, 15-minute placeholder grace, media-source filter 1.</summary>
    /// <param name="fixture">The seeded test database.</param>
    /// <param name="channelId">The channel the rule is created on.</param>
    /// <param name="genreIds">The genre ids of the rotation.</param>
    /// <param name="clipGroupIds">The clip group ids of the rotation.</param>
    /// <returns>The created rule.</returns>
    public static Task<ProgramRule> CreateRuleAsync(
        ProgramScheduleTestDb fixture, long channelId,
        IEnumerable<long> genreIds, IEnumerable<long> clipGroupIds)
        => fixture.CreateRuleAsync(channelId, "Primetime", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, Start, ProgramRuleMaterialType.Genre, 0,
            slotDuration: Period, genreIds: genreIds, mediaSourceIds: [1],
            clipGroupIds: clipGroupIds, placeholderGrace: Grace);

    /// <summary>Seeds a genre on source 1 with <paramref name="movieCount"/> movies of
    /// equal duration, all linked to the genre.</summary>
    /// <param name="Genre">The seeded genre (return tuple element).</param>
    /// <param name="Movies">The seeded movies (return tuple element).</param>
    /// <param name="fixture">The seeded test database.</param>
    /// <param name="name">The genre name; the movies are named after it.</param>
    /// <param name="movieCount">How many movies to seed.</param>
    /// <param name="duration">The runtime of every movie.</param>
    /// <param name="minAge">Optional minimum age the movies carry for the age window.</param>
    /// <returns>The genre and its movies.</returns>
    public static async Task<(Genre Genre, List<Movie> Movies)> AddMovieGenreAsync(
        ProgramScheduleTestDb fixture, string name, int movieCount, TimeSpan duration,
        int? minAge = null)
    {
        var genre = await fixture.CreateGenreAsync(name);
        var movies = new List<Movie>();
        for (var i = 1; i <= movieCount; i++)
        {
            var movie = await fixture.CreateMovieAsync($"{name}-Film{i}", duration: duration, ageRatingMinAge: minAge);
            await fixture.LinkMovieGenreAsync(movie.Id, genre.Id);
            movies.Add(movie);
        }
        return (genre, movies);
    }

    /// <summary>Seeds a genre on source 1 with one show whose episodes all share the
    /// given duration, linked to the genre.</summary>
    /// <param name="Genre">The seeded genre (return tuple element).</param>
    /// <param name="Show">The seeded show (return tuple element).</param>
    /// <param name="Episodes">The seeded episodes in air order (return tuple element).</param>
    /// <param name="fixture">The seeded test database.</param>
    /// <param name="genreName">The genre name.</param>
    /// <param name="showName">The show name.</param>
    /// <param name="episodeCount">How many episodes to seed.</param>
    /// <param name="duration">The runtime of every episode.</param>
    /// <param name="minAge">Optional minimum age the show carries for the age window.</param>
    /// <returns>The genre, the show and its episodes.</returns>
    public static async Task<(Genre Genre, TVShow Show, List<TVShowEpisode> Episodes)> AddShowGenreAsync(
        ProgramScheduleTestDb fixture, string genreName, string showName, int episodeCount,
        TimeSpan duration, int? minAge = null)
    {
        var genre = await fixture.CreateGenreAsync(genreName);
        var (show, _, episodes) = await fixture.CreateShowAsync(showName, episodeCount, duration: duration, ageRatingMinAge: minAge);
        await fixture.LinkShowGenreAsync(show.Id, genre.Id);
        return (genre, show, episodes);
    }

    /// <summary>Seeds a user clip group holding <paramref name="clipCount"/> bare
    /// (unclassified) media items of equal duration.</summary>
    /// <param name="Group">The seeded clip group (return tuple element).</param>
    /// <param name="Items">The seeded media items (return tuple element).</param>
    /// <param name="fixture">The seeded test database.</param>
    /// <param name="name">The clip group name.</param>
    /// <param name="clipCount">How many items the group holds.</param>
    /// <param name="duration">The runtime of every item.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The clip group and its items.</returns>
    public static async Task<(ProgramClipGroup Group, List<MediaItem> Items)> AddClipGroupAsync(
        ProgramScheduleTestDb fixture, string name, int clipCount, TimeSpan duration, CancellationToken ct)
    {
        var group = await fixture.CreateClipGroupAsync(name, ct);
        var items = new List<MediaItem>();
        for (var i = 0; i < clipCount; i++)
        {
            var item = await fixture.CreateMediaItemAsync(duration: duration);
            await fixture.AddClipToGroupAsync(group.Id, item.Id, ct);
            items.Add(item);
        }
        return (group, items);
    }

    /// <summary>Seeds a trailer link (<see cref="MovieMediaItem.IsTrailer"/>) of the
    /// given movie — its media item stays unclassified for the clip candidate filter.</summary>
    /// <param name="fixture">The seeded test database.</param>
    /// <param name="movie">The movie the trailer belongs to.</param>
    /// <param name="duration">The runtime of the trailer item.</param>
    /// <returns>The trailer's media item.</returns>
    public static async Task<MediaItem> AddTrailerAsync(
        ProgramScheduleTestDb fixture, Movie movie, TimeSpan duration)
    {
        var item = await fixture.CreateMediaItemAsync(duration: duration);
        fixture.Db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id, IsTrailer = true });
        await fixture.SaveDiagAsync();
        return item;
    }

    /// <summary>One worker iteration of the simulation: reconcile first, then extend
    /// coverage — the same order <see cref="ProgramScheduleWorker"/> runs them.</summary>
    /// <param name="Reconcile">The reconcile summary of the iteration (return tuple element).</param>
    /// <param name="Coverage">The coverage-run summary of the iteration (return tuple element).</param>
    /// <param name="fixture">The seeded test database.</param>
    /// <param name="now">The simulated current time.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reconcile and coverage summaries of the iteration.</returns>
    public static async Task<(ProgramScheduleReconcileSummary Reconcile, ProgramScheduleRunSummary Coverage)> StepAsync(
        ProgramScheduleTestDb fixture, DateTimeOffset now, CancellationToken ct)
    {
        var service = fixture.CreateService();
        var reconcile = await service.ReconcileAsync(now, ct);
        var coverage = await service.EnsureCoverageAsync(now, ct);
        return (reconcile, coverage);
    }

    /// <summary>Full-inventory preservation check: every previously scheduled slot must
    /// still exist unchanged (start, end, content, rule, clip-fill flag) — no displacement,
    /// no re-materialization, no silent rewrite.</summary>
    /// <param name="before">The slots captured before the run.</param>
    /// <param name="after">The slots captured after the run.</param>
    /// <param name="context">Label for assertion messages (e.g. the simulated day).</param>
    public static void AssertPreserved(
        IReadOnlyCollection<ProgramSlot> before, IReadOnlyCollection<ProgramSlot> after, string context)
    {
        var afterById = after.Where(s => s.Status == ProgramSlotStatus.Scheduled).ToDictionary(s => s.Id);
        foreach (var prev in before.Where(s => s.Status == ProgramSlotStatus.Scheduled))
        {
            Assert.True(afterById.TryGetValue(prev.Id, out var cur),
                $"{context}: Slot {prev.Id} ({ToLocal(prev.Start):dd.MM. HH:mm}-{ToLocal(prev.End):HH:mm}, Item {prev.MediaItemId}) fehlt oder wurde verdrängt.");
            Assert.Equal(prev.Start, cur!.Start);
            Assert.Equal(prev.End, cur.End);
            Assert.Equal(prev.MediaItemId, cur.MediaItemId);
            Assert.Equal(prev.MovieId, cur.MovieId);
            Assert.Equal(prev.TVShowEpisodeId, cur.TVShowEpisodeId);
            Assert.Equal(prev.ProgramRuleId, cur.ProgramRuleId);
            Assert.Equal(prev.IsClipFill, cur.IsClipFill);
        }
    }

    /// <summary>Negative-space check: no slot of the rule may start outside the
    /// 20:15–24:00 local window. Continuations past midnight are legal (they started
    /// inside the window); new starts are not.</summary>
    /// <param name="slots">The rule slots to check.</param>
    public static void AssertStartsInsidePeriod(IEnumerable<ProgramSlot> slots)
    {
        foreach (var slot in slots)
        {
            Assert.True(LocalMinutesOfDay(slot.Start) >= PrimetimeStartOfDay.TotalMinutes,
                $"Slot {slot.Id} (Item {slot.MediaItemId}) startet {ToLocal(slot.Start):dd.MM. HH:mm} lokal — außerhalb der Primetime.");
        }
    }

    /// <summary>The airing of one evening: all slots of the rule whose start belongs to
    /// that evening (see <see cref="EveningDateOf"/>).</summary>
    /// <param name="ruleSlots">The slots of the rule.</param>
    /// <param name="evening">The local evening date.</param>
    /// <returns>The slots of that evening's airing, ordered by start.</returns>
    public static List<ProgramSlot> AiringOf(IEnumerable<ProgramSlot> ruleSlots, DateOnly evening)
        => ruleSlots.Where(s => EveningDateOf(s.Start) == evening).OrderBy(s => s.Start).ToList();

    /// <summary>Positive check: every expected evening carries an airing with real
    /// content — at least one movie or episode slot, not just clips.</summary>
    /// <param name="ruleSlots">The slots of the rule.</param>
    /// <param name="firstEvening">The first expected evening.</param>
    /// <param name="evenings">How many consecutive evenings to check.</param>
    public static void AssertEveryEveningHasTitles(IEnumerable<ProgramSlot> ruleSlots, DateOnly firstEvening, int evenings)
    {
        for (var i = 0; i < evenings; i++)
        {
            var evening = firstEvening.AddDays(i);
            var airing = AiringOf(ruleSlots, evening);
            Assert.NotEmpty(airing);
            Assert.Contains(airing, s => s.MovieId is not null || s.TVShowEpisodeId is not null);
        }
    }
}
