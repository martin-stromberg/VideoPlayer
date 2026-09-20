using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for the central marking hook in <see cref="ApplicationDbContext"/> (SaveChanges override): creating a
/// child of a TV show, season or movie collection persistently marks that collection medium for the automatic
/// playlist backfill. Runs against real SQLite (unique index, transactions).
/// </summary>
public class PlaylistBackfillMarkerHookTests : PlaylistServiceTestBase
{
    private ApplicationDbContext NewContext(IPlaylistBackfillSignal? signal = null, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(interceptors)
            .Options;
        return new ApplicationDbContext(options, new EventManager(), signal);
    }

    private static List<(string MediaType, long MediaId)> Refs(IEnumerable<PlaylistBackfillMarker> markers)
        => markers.Select(m => (m.MediaType, m.MediaId)).OrderBy(m => m.MediaType).ThenBy(m => m.MediaId).ToList();

    [Fact]
    public async Task NewEpisodeInExistingSeason_MarksSeasonAndShow_WithTrackedSeason()
    {
        var (showId, seasonId, _) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        await ClearMarkersAsync();

        // The season is still tracked by _db from the arrangement, so the show is known without a query.
        _db.TVShowEpisodes.Add(new TVShowEpisode { Name = "Neu", TVShowSeasonId = seasonId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = 9 });
        await _db.SaveChangesAsync();

        Assert.Equal(
            new List<(string, long)> { (MediaTypeValues.TVShow, showId), (MediaTypeValues.TVShowSeason, seasonId) },
            Refs(await GetMarkersAsync()));
    }

    [Fact]
    public async Task NewEpisodeInExistingSeason_MarksSeasonAndShow_WithUntrackedSeason()
    {
        var (showId, seasonId, _) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        await ClearMarkersAsync();

        var recorder = new SqlCommandRecorder();
        await using var scanDb = NewContext(null, recorder);
        scanDb.TVShowEpisodes.Add(new TVShowEpisode { Name = "Neu", TVShowSeasonId = seasonId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = 9 });
        await scanDb.SaveChangesAsync();

        Assert.Equal(
            new List<(string, long)> { (MediaTypeValues.TVShow, showId), (MediaTypeValues.TVShowSeason, seasonId) },
            Refs(await GetMarkersAsync()));
        Assert.Single(recorder.Commands, c => c.Contains("\"TVShowSeasons\"") && c.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NewSeasonOfExistingShow_MarksOnlyTheShow()
    {
        var (showId, _, _) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        await ClearMarkersAsync();

        await AddSeasonToShowAsync(showId, "Staffel 2", 0);

        Assert.Equal(new List<(string, long)> { (MediaTypeValues.TVShow, showId) }, Refs(await GetMarkersAsync()));
    }

    [Fact]
    public async Task NewMovieInCollection_MarksTheCollection()
    {
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        await ClearMarkersAsync();

        await AddMovieToCollectionAsync(collectionId, "Film 2");

        Assert.Equal(new List<(string, long)> { (MediaTypeValues.MovieCollection, collectionId) }, Refs(await GetMarkersAsync()));
    }

    [Fact]
    public async Task MovieMovedToOtherCollection_MarksTheNewCollection()
    {
        var oldCollectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Alt");
        var newCollectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Neu");
        var movieId = await AddMovieToCollectionAsync(oldCollectionId, "Film");
        await ClearMarkersAsync();

        await using var editDb = NewContext();
        var movie = await editDb.Movies.SingleAsync(m => m.Id == movieId);
        movie.MovieCollectionId = newCollectionId;
        await editDb.SaveChangesAsync();

        Assert.Equal(new List<(string, long)> { (MediaTypeValues.MovieCollection, newCollectionId) }, Refs(await GetMarkersAsync()));
    }

    [Fact]
    public async Task ChangesThatCreateNoChild_LeaveNoMarker()
    {
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var movieId = await AddMovieToCollectionAsync(collectionId, "Film");
        var (_, _, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        await ClearMarkersAsync();

        await using var editDb = NewContext();
        (await editDb.Movies.SingleAsync(m => m.Id == movieId)).Name = "Umbenannt";
        (await editDb.TVShowEpisodes.SingleAsync(e => e.Id == episodeIds[0])).Name = "Umbenannt";
        editDb.Movies.Add(new Movie { Name = "Einzelfilm ohne Sammlung", MediaSourceId = 1, CreatedAt = DateTime.UtcNow });
        editDb.TVShows.Add(new TVShow { Name = "Andere Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow });
        await editDb.SaveChangesAsync();

        Assert.Empty(await GetMarkersAsync());
    }

    [Fact]
    public async Task ParentCreatedInSameSave_IsNotMarked()
    {
        await ClearMarkersAsync();

        var show = new TVShow { Name = "Neue Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        var season = new TVShowSeason { Name = "Staffel 1", TVShow = show, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        var episode = new TVShowEpisode { Name = "Folge 1", TVShowSeason = season, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = 1 };
        var collection = new MovieCollection { Name = "Neue Sammlung", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        var movie = new Movie { Name = "Film", MovieCollection = collection, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.AddRange(show, season, episode, collection, movie);
        await _db.SaveChangesAsync();

        Assert.Empty(await GetMarkersAsync());
    }

    [Fact]
    public async Task MarkingTheSameCollectionRepeatedly_IsIdempotentAndCountsVersions()
    {
        var (showId, seasonId, _) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 0);
        await ClearMarkersAsync();

        for (var i = 1; i <= 3; i++)
        {
            _db.TVShowEpisodes.Add(new TVShowEpisode { Name = $"Folge {i}", TVShowSeasonId = seasonId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = i });
            await _db.SaveChangesAsync();
        }

        var markers = await GetMarkersAsync();
        Assert.Equal(2, markers.Count);
        Assert.All(markers, m => Assert.Equal(3, m.Version));
        Assert.Contains(markers, m => m.MediaType == MediaTypeValues.TVShow && m.MediaId == showId);
    }

    [Fact]
    public async Task ManyEpisodesInOneSave_WriteFewCommandsAndDeduplicatedMarkers()
    {
        var (showAId, season1Id, _) = await CreateShowWithSeasonAsync("Serie A", "Staffel 1", 0);
        var (season2Id, _) = await AddSeasonToShowAsync(showAId, "Staffel 2", 0);
        var (showBId, season3Id, _) = await CreateShowWithSeasonAsync("Serie B", "Staffel 1", 0);
        await ClearMarkersAsync();

        var recorder = new SqlCommandRecorder();
        await using var scanDb = NewContext(null, recorder);
        foreach (var seasonId in new[] { season1Id, season2Id, season3Id })
        {
            for (var i = 1; i <= 200; i++)
                scanDb.TVShowEpisodes.Add(new TVShowEpisode { Name = $"Folge {seasonId}-{i}", TVShowSeasonId = seasonId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = i });
        }

        await scanDb.SaveChangesAsync();

        // 600 episodes, but only 3 seasons + 2 shows are marked, with one lookup and one bundled upsert.
        Assert.Equal(600, await _db.TVShowEpisodes.AsNoTracking().CountAsync());
        var markers = await GetMarkersAsync();
        Assert.Equal(5, markers.Count);
        Assert.All(markers, m => Assert.Equal(1, m.Version));
        Assert.Contains(markers, m => m.MediaType == MediaTypeValues.TVShow && m.MediaId == showBId);
        Assert.Single(recorder.Commands, c => c.Contains("\"TVShowSeasons\"") && c.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.Single(recorder.Commands, c => c.Contains("INSERT INTO \"PlaylistBackfillMarkers\""));
    }

    [Fact]
    public async Task SaveWithoutRelevantEntities_IssuesNoMarkerCommand()
    {
        await GetOrCreateGenreIdAsync("Basis"); // ensures the media source the genre refers to
        var recorder = new SqlCommandRecorder();
        await using var scanDb = NewContext(null, recorder);
        scanDb.Genres.Add(new Genre { MediaSourceId = 1, Name = "Drama" });
        scanDb.TVShows.Add(new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow });
        await scanDb.SaveChangesAsync();

        Assert.DoesNotContain(recorder.Commands, c => c.Contains("PlaylistBackfillMarkers") || c.Contains("\"TVShowSeasons\""));
    }

    [Fact]
    public async Task RolledBackTransaction_LeavesNoMarkerBehind()
    {
        var (_, seasonId, _) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        await ClearMarkersAsync();
        var episodeCountBefore = await _db.TVShowEpisodes.AsNoTracking().CountAsync();

        await using var scanDb = NewContext();
        await using (var transaction = await scanDb.Database.BeginTransactionAsync())
        {
            scanDb.TVShowEpisodes.Add(new TVShowEpisode { Name = "Neu", TVShowSeasonId = seasonId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = 9 });
            await scanDb.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.Empty(await GetMarkersAsync());
        Assert.Equal(episodeCountBefore, await _db.TVShowEpisodes.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task FailingMarkerWrite_RollsBackTheNewMediumToo()
    {
        var (_, seasonId, _) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        await _db.Database.ExecuteSqlRawAsync("DROP TABLE \"PlaylistBackfillMarkers\";");
        var episodeCountBefore = await _db.TVShowEpisodes.AsNoTracking().CountAsync();

        await using var scanDb = NewContext();
        scanDb.TVShowEpisodes.Add(new TVShowEpisode { Name = "Neu", TVShowSeasonId = seasonId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = 9 });

        await Assert.ThrowsAnyAsync<Exception>(() => scanDb.SaveChangesAsync());

        // Media row and marker are one unit: the failed marker write took the episode down with it.
        Assert.Equal(episodeCountBefore, await _db.TVShowEpisodes.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task FailingSaveOfTheMedium_LeavesNoMarker()
    {
        await ClearMarkersAsync();

        await using var scanDb = NewContext();
        // Season 99999 does not exist: the foreign key fails and the save throws.
        scanDb.TVShowEpisodes.Add(new TVShowEpisode { Name = "Waise", TVShowSeasonId = 99999, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = 1 });

        await Assert.ThrowsAnyAsync<Exception>(() => scanDb.SaveChangesAsync());

        Assert.Empty(await GetMarkersAsync());
    }

    [Fact]
    public async Task Marker_SurvivesRestart_NewContextSeesIt()
    {
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        await ClearMarkersAsync();
        await AddMovieToCollectionAsync(collectionId, "Film");

        await using var afterRestart = NewContext();
        var marker = await afterRestart.PlaylistBackfillMarkers.AsNoTracking().SingleAsync();

        Assert.Equal(MediaTypeValues.MovieCollection, marker.MediaType);
        Assert.Equal(collectionId, marker.MediaId);
    }

    [Fact]
    public async Task InMemoryProvider_StillMarks_ThroughTheNonRelationalFallback()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"marker-hook-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var memoryDb = new ApplicationDbContext(options, new EventManager());
        memoryDb.Movies.Add(new Movie { Name = "Film", MovieCollectionId = 5, MediaSourceId = 1, CreatedAt = DateTime.UtcNow });
        await memoryDb.SaveChangesAsync();
        memoryDb.Movies.Add(new Movie { Name = "Film 2", MovieCollectionId = 5, MediaSourceId = 1, CreatedAt = DateTime.UtcNow });
        await memoryDb.SaveChangesAsync();

        var marker = await memoryDb.PlaylistBackfillMarkers.AsNoTracking().SingleAsync();
        Assert.Equal(MediaTypeValues.MovieCollection, marker.MediaType);
        Assert.Equal(2, marker.Version);
    }

    [Fact]
    public async Task WrittenMarkers_NotifyTheSignal_ButNothingMarkedDoesNot()
    {
        var signal = new CountingSignal();
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        await GetOrCreateGenreIdAsync("Basis"); // ensures the media source the genre refers to

        await using var scanDb = NewContext(signal);
        scanDb.Genres.Add(new Genre { MediaSourceId = 1, Name = "Drama" });
        await scanDb.SaveChangesAsync();
        Assert.Equal(0, signal.Notifications);

        scanDb.Movies.Add(new Movie { Name = "Film", MovieCollectionId = collectionId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow });
        await scanDb.SaveChangesAsync();
        Assert.Equal(1, signal.Notifications);
    }

    private sealed class CountingSignal : IPlaylistBackfillSignal
    {
        public int Notifications { get; private set; }

        public void NotifyMarkersWritten() => Notifications++;

        public IDisposable BeginScan() => throw new NotSupportedException();

        public ValueTask WaitAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
