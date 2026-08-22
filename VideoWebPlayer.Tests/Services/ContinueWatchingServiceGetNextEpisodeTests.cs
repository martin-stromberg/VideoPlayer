using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests für die Ermittlung der nächsten Episode in <see cref="VideoWebPlayer.Services.ContinueWatchingService"/>.
/// </summary>
public class ContinueWatchingServiceGetNextEpisodeTests : ContinueWatchingServiceTestBase
{
    private Task<TVShow> CreateTestShowWithSeasons(params (string Name, (int Number, DateTime? ReleaseDate)[] Episodes)[] seasons)
        => TestHelpers.CreateTvShowWithSeasonsAsync(_db, seasons);

    private static void AssertEpisodeEquals(TVShowEpisode expected, TVShowEpisode? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual!.Id);
        Assert.Equal(expected.Number, actual.Number);
        Assert.Equal(expected.ReleaseDate, actual.ReleaseDate);
    }

    [Fact]
    public async Task HappyPath_SimpleEpisodeSequence_ReturnsNextEpisode()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, new DateTime(2020, 1, 1)), (2, new DateTime(2020, 1, 8)) }));

        var episode1 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 1);
        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode1.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entry);
        var next = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == entry!.TVShowEpisodeId);
        AssertEpisodeEquals(episode2, next);
    }

    [Fact]
    public async Task AllEpisodesWithoutReleaseDate_SortsByNumber()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null), (3, null) }));

        var episode1 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 1);
        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode1.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entry);
        var next = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == entry!.TVShowEpisodeId);
        AssertEpisodeEquals(episode2, next);
    }

    [Fact]
    public async Task AllEpisodesWithReleaseDate_SortsCorrectly()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[]
            {
                (1, new DateTime(2020, 3, 1)),
                (2, new DateTime(2020, 2, 1)),
                (3, new DateTime(2020, 1, 1)),
            }));

        var episode1 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 1);
        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode1.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entry);
        var next = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == entry!.TVShowEpisodeId);
        AssertEpisodeEquals(episode2, next);
    }

    [Fact]
    public async Task MixedReleaseDate_NullAndNonNull_SortsConsistently()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[]
            {
                (1, new DateTime(2020, 1, 1)),
                (2, null),
                (3, new DateTime(2020, 3, 1)),
            }));

        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);
        var episode3 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 3);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode2.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entry);
        var next = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == entry!.TVShowEpisodeId);
        AssertEpisodeEquals(episode3, next);
    }

    [Fact]
    public async Task EpisodeGaps_SkipsGappedEpisodes_FindsNext()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null), (4, null), (5, null) }));

        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);
        var episode4 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 4);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode2.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entry);
        var next = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == entry!.TVShowEpisodeId);
        AssertEpisodeEquals(episode4, next);
    }

    [Fact]
    public async Task FirstEpisodeMissing_SkipsTo_NextAvailable()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (2, null), (3, null), (4, null) }));

        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);
        var episode3 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 3);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode2.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entry);
        var next = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == entry!.TVShowEpisodeId);
        AssertEpisodeEquals(episode3, next);
    }

    [Fact]
    public async Task LastEpisodeOfSeason_ReturnsNull_InSameSeason()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null) }));

        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode2.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.Null(entry);
    }

    [Fact]
    public async Task SeasonTransition_LastEpisodeOfSeason_JumpsToNextSeason()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null) }),
            ("Staffel 02", new (int, DateTime?)[] { (1, null) }));

        var season1Episode2 = await _db.TVShowEpisodes
            .Include(e => e.TVShowSeason)
            .FirstAsync(e => e.Number == 2 && e.TVShowSeason.Name == "Staffel 01");
        var season2Episode1 = await _db.TVShowEpisodes
            .Include(e => e.TVShowSeason)
            .FirstAsync(e => e.Number == 1 && e.TVShowSeason.Name == "Staffel 02");

        await _service.ProcessBufferedEntryAsync(_testUserId, null, season1Episode2.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entry);
        var next = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == entry!.TVShowEpisodeId);
        AssertEpisodeEquals(season2Episode1, next);
    }

    [Fact]
    public async Task NoNextSeason_ReturnsNull()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null), (3, null) }));

        var episode3 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 3);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode3.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.Null(entry);
    }

    [Fact]
    public async Task NextSeasonEmpty_ReturnsNull()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null) }),
            ("Staffel 02", Array.Empty<(int, DateTime?)>()));

        var season1Episode2 = await _db.TVShowEpisodes
            .Include(e => e.TVShowSeason)
            .FirstAsync(e => e.Number == 2 && e.TVShowSeason.Name == "Staffel 01");

        await _service.ProcessBufferedEntryAsync(_testUserId, null, season1Episode2.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.Null(entry);
    }

    [Fact]
    public async Task SingleEpisodeInSeason_ReturnsNull()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, null) }));

        var episode1 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 1);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode1.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.Null(entry);
    }

    [Fact]
    public async Task MultipleEpisodesWithIdenticalReleaseDate_SortsByNumber()
    {
        var sharedDate = new DateTime(2020, 1, 1);
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, sharedDate), (2, sharedDate), (3, sharedDate) }));

        var episode1 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 1);
        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode1.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entry);
        var next = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == entry!.TVShowEpisodeId);
        AssertEpisodeEquals(episode2, next);
    }

    [Fact]
    public async Task RegressionTest_LoopScenario_NoInfiniteLoop()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null), (3, null) }));

        var episodeA = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 1);
        var episodeB = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);
        var episodeC = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 3);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episodeA.Id, CompletedPosition, Duration);
        var entryAfterA = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entryAfterA);
        Assert.Equal(episodeB.Id, entryAfterA!.TVShowEpisodeId);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episodeB.Id, CompletedPosition, Duration);
        var entryAfterB = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entryAfterB);
        Assert.Equal(episodeC.Id, entryAfterB!.TVShowEpisodeId);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episodeC.Id, CompletedPosition, Duration);
        var entryAfterC = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.Null(entryAfterC);
    }

    [Fact]
    public async Task OffByOne_PositionNotConfusedWithId()
    {
        await CreateTestShowWithSeasons(
            ("Staffel 01", new (int, DateTime?)[] { (10, null), (5, null), (15, null) }));

        var episode5 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 5);
        var episode10 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 10);

        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode5.Id, CompletedPosition, Duration);

        var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(x => x.UserId == _testUserId);
        Assert.NotNull(entry);
        var next = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == entry!.TVShowEpisodeId);
        AssertEpisodeEquals(episode10, next);
    }
}
