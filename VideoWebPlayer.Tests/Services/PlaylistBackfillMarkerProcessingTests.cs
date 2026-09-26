using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for the marker-driven processing of <see cref="PlaylistBackfillService"/>: only the playlists that
/// contain a marked collection medium are examined, processed markers are removed (but never a marker set
/// meanwhile, and never that of a failed playlist).
/// </summary>
public class PlaylistBackfillMarkerProcessingTests : PlaylistServiceTestBase
{
    private async Task<long> CreateShowPlaylistAsync(string name)
    {
        var (showId, seasonId, episodeIds) = await CreateShowWithSeasonAsync(name, "Staffel 1", 1);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShow, showId),
            (MediaTypeValues.TVShowSeason, seasonId),
            (MediaTypeValues.TVShowEpisode, episodeIds[0]));
        return playlistId;
    }

    private async Task<HashSet<long>> EpisodeIdsInPlaylistAsync(long playlistId)
        => (await _db.PlaylistEntries.AsNoTracking()
                .Where(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.TVShowEpisode)
                .Select(e => e.MediaId)
                .ToListAsync())
            .ToHashSet();

    // EF1002 is suppressed deliberately: SQLite cannot take parameters in DDL (CREATE/DROP TRIGGER), so the
    // statement has to be built as text. The only interpolated values are numeric ids created by this test itself.
#pragma warning disable EF1002
    private async Task FailInsertsForPlaylistAsync(long playlistId)
        => await _db.Database.ExecuteSqlRawAsync(
            $"CREATE TRIGGER fail_playlist_{playlistId} BEFORE INSERT ON \"PlaylistEntries\" WHEN NEW.\"PlaylistId\" = {playlistId} BEGIN SELECT RAISE(ABORT, 'boom'); END;");

    private async Task DropFailTriggerAsync(long playlistId)
        => await _db.Database.ExecuteSqlRawAsync($"DROP TRIGGER fail_playlist_{playlistId};");
#pragma warning restore EF1002

    [Fact]
    public async Task OnlyPlaylistsWithTheMarkedCollectionAreProcessed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showAId, seasonAId, epA) = await CreateShowWithSeasonAsync("Serie A", "Staffel 1", 1);
        var (showBId, seasonBId, epB) = await CreateShowWithSeasonAsync("Serie B", "Staffel 1", 1);
        var playlistA = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showAId), (MediaTypeValues.TVShowSeason, seasonAId), (MediaTypeValues.TVShowEpisode, epA[0]));
        var playlistB = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showBId), (MediaTypeValues.TVShowSeason, seasonBId), (MediaTypeValues.TVShowEpisode, epB[0]));
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        await ClearMarkersAsync();

        // Both shows get a new episode, but the mark of show B is lost (as if a path had bypassed the hook).
        var (_, newEpA) = await AddSeasonToShowAsync(showAId, "Staffel 2", 1);
        var (_, newEpB) = await AddSeasonToShowAsync(showBId, "Staffel 2", 1);
        await _db.PlaylistBackfillMarkers.Where(m => m.MediaType == MediaTypeValues.TVShow && m.MediaId == showBId).ExecuteDeleteAsync(ct);

        var service = CreateBackfillService();
        var plan = await service.PlanPendingAsync(ct);
        var result = await service.BackfillPlaylistsAsync(plan.PlaylistIds, ct);

        // Only playlist A is examined; playlist B (which WOULD have received its new episode if every
        // playlist were checked) and the playlist without a collection entry are not touched.
        Assert.Equal(new[] { playlistA }, plan.PlaylistIds);
        Assert.Equal(1, result.PlaylistsExamined);
        Assert.Contains(newEpA[0], await EpisodeIdsInPlaylistAsync(playlistA));
        Assert.DoesNotContain(newEpB[0], await EpisodeIdsInPlaylistAsync(playlistB));

        // The safety sweep covers what the lost mark left behind.
        var sweepBlock = await service.LoadSweepBlockAsync(0, 10, ct);
        Assert.Contains(playlistB, sweepBlock);
        await service.BackfillPlaylistsAsync(sweepBlock, ct);
        Assert.Contains(newEpB[0], await EpisodeIdsInPlaylistAsync(playlistB));
    }

    [Fact]
    public async Task NothingMarked_CostsOneQueryAndNoPlaylistIsLoaded()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateShowPlaylistAsync("Serie");
        await ClearMarkersAsync();

        var recorder = new SqlCommandRecorder();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connectionString).AddInterceptors(recorder).Options;
        await using var probeDb = new ApplicationDbContext(options, new EventManager());
        var service = new PlaylistBackfillService(probeDb, Microsoft.Extensions.Options.Options.Create(new PlaylistSettings()));

        var plan = await service.PlanPendingAsync(ct);

        Assert.True(plan.IsEmpty);
        Assert.Empty(plan.PlaylistIds);
        Assert.Single(recorder.Commands);
        Assert.Contains("PlaylistBackfillMarkers", recorder.Commands[0]);
    }

    [Fact]
    public async Task ProcessedMarkers_AreRemoved_AlsoThoseWithoutAnyPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateShowPlaylistAsync("Serie mit Playlist");
        var (orphanShowId, _, _) = await CreateShowWithSeasonAsync("Serie ohne Playlist", "Staffel 1", 1);
        await ClearMarkersAsync();
        // Marks show + season of the first show, and show + season of the show no playlist contains.
        await AddSeasonToShowAsync(await _db.TVShows.AsNoTracking().Where(s => s.Name == "Serie mit Playlist").Select(s => s.Id).SingleAsync(ct), "Staffel 2", 1);
        await AddSeasonToShowAsync(orphanShowId, "Staffel 2", 1);
        Assert.NotEmpty(await GetMarkersAsync());

        var result = await RunPendingBackfillAsync(CreateBackfillService(), ct);

        Assert.Equal(1, result.PlaylistsExamined);
        Assert.Empty(await GetMarkersAsync());
        Assert.Equal(2, (await EpisodeIdsInPlaylistAsync(playlistId)).Count);
    }

    [Fact]
    public async Task MarkerSetWhileProcessing_IsNotLost()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateShowPlaylistAsync("Serie");
        var showId = await _db.TVShows.AsNoTracking().Select(s => s.Id).SingleAsync(ct);
        await ClearMarkersAsync();
        var (_, firstNewEp) = await AddSeasonToShowAsync(showId, "Staffel 2", 1);

        var service = CreateBackfillService();
        var plan = await service.PlanPendingAsync(ct);
        var result = await service.BackfillPlaylistsAsync(plan.PlaylistIds, ct);

        // A new season shows up while the run is still going (after the plan was made).
        var (_, secondNewEp) = await AddSeasonToShowAsync(showId, "Staffel 3", 1);
        await service.ReleaseMarkersAsync(plan, result.FailedPlaylistIds, ct);

        var remaining = await GetMarkersAsync();
        var showMarker = Assert.Single(remaining, m => m.MediaType == MediaTypeValues.TVShow && m.MediaId == showId);
        Assert.True(showMarker.Version > plan.Markers.Single(m => m.Id == showMarker.Id).Version);
        Assert.DoesNotContain(secondNewEp[0], await EpisodeIdsInPlaylistAsync(playlistId));

        // The next run delivers it and only then removes the marker.
        await RunPendingBackfillAsync(service, ct);
        Assert.Contains(secondNewEp[0], await EpisodeIdsInPlaylistAsync(playlistId));
        Assert.Contains(firstNewEp[0], await EpisodeIdsInPlaylistAsync(playlistId));
        Assert.Empty(await GetMarkersAsync());
    }

    [Fact]
    public async Task FailingPlaylist_DoesNotBlockTheOthers_AndKeepsItsMarker()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showAId, seasonAId, epA) = await CreateShowWithSeasonAsync("Serie A", "Staffel 1", 1);
        var (showBId, seasonBId, epB) = await CreateShowWithSeasonAsync("Serie B", "Staffel 1", 1);
        var playlistA = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showAId), (MediaTypeValues.TVShowSeason, seasonAId), (MediaTypeValues.TVShowEpisode, epA[0]));
        var playlistB = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showBId), (MediaTypeValues.TVShowSeason, seasonBId), (MediaTypeValues.TVShowEpisode, epB[0]));
        await ClearMarkersAsync();
        var (_, newEpA) = await AddSeasonToShowAsync(showAId, "Staffel 2", 1);
        var (_, newEpB) = await AddSeasonToShowAsync(showBId, "Staffel 2", 1);
        await FailInsertsForPlaylistAsync(playlistA);

        var service = CreateBackfillService();
        var result = await RunPendingBackfillAsync(service, ct);

        Assert.Equal(new[] { playlistA }, result.FailedPlaylistIds);
        Assert.Equal(2, result.PlaylistsExamined);
        Assert.Contains(newEpB[0], await EpisodeIdsInPlaylistAsync(playlistB));
        Assert.DoesNotContain(newEpA[0], await EpisodeIdsInPlaylistAsync(playlistA));
        var remaining = await GetMarkersAsync();
        Assert.All(remaining, m => Assert.True(m.MediaId == showAId || m.MediaType == MediaTypeValues.TVShowSeason));
        Assert.Contains(remaining, m => m.MediaType == MediaTypeValues.TVShow && m.MediaId == showAId);
        Assert.DoesNotContain(remaining, m => m.MediaType == MediaTypeValues.TVShow && m.MediaId == showBId);

        // Next attempt (the fault is gone) delivers to A and finally clears the marker.
        await DropFailTriggerAsync(playlistA);
        await RunPendingBackfillAsync(service, ct);
        Assert.Contains(newEpA[0], await EpisodeIdsInPlaylistAsync(playlistA));
        Assert.Empty(await GetMarkersAsync());
    }

    [Fact]
    public async Task MarkerSharedByTwoPlaylists_StaysWhileOneOfThemFails()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, seasonId, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        var entries = new[] { (MediaTypeValues.TVShow, showId), (MediaTypeValues.TVShowSeason, seasonId), (MediaTypeValues.TVShowEpisode, episodeIds[0]) };
        var failing = await CreateTestPlaylistWithEntriesAsync(_testUserId, entries);
        var healthy = await CreateTestPlaylistWithEntriesAsync(_testUserId, entries);
        await ClearMarkersAsync();
        var (_, newEp) = await AddSeasonToShowAsync(showId, "Staffel 2", 1);
        await FailInsertsForPlaylistAsync(failing);

        await RunPendingBackfillAsync(CreateBackfillService(), ct);

        Assert.Contains(newEp[0], await EpisodeIdsInPlaylistAsync(healthy));
        Assert.DoesNotContain(newEp[0], await EpisodeIdsInPlaylistAsync(failing));
        Assert.Contains(await GetMarkersAsync(), m => m.MediaType == MediaTypeValues.TVShow && m.MediaId == showId);
    }

    [Fact]
    public async Task MovieMovedIntoCollection_IsDeliveredToPlaylistsContainingThatCollection()
    {
        var ct = TestContext.Current.CancellationToken;
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var firstMovieId = await AddMovieToCollectionAsync(collectionId, "Film 1");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.MovieCollection, collectionId), (MediaTypeValues.Movie, firstMovieId));
        var looseMovieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Loser Film");
        await ClearMarkersAsync();

        var movie = await _db.Movies.SingleAsync(m => m.Id == looseMovieId, ct);
        movie.MovieCollectionId = collectionId;
        await _db.SaveChangesAsync(ct);

        var result = await RunPendingBackfillAsync(CreateBackfillService(), ct);

        Assert.Equal(1, result.EntriesAdded);
        Assert.True(await _db.PlaylistEntries.AsNoTracking().AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == looseMovieId, ct));
    }
}
