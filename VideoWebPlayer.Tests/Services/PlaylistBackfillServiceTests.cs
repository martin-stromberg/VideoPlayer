using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistBackfillService"/> (Entwicklungsschritt 8): playlists
/// containing a complete TV show, season or movie collection automatically pick up newly added children.
/// </summary>
public class PlaylistBackfillServiceTests : PlaylistServiceTestBase
{
    [Fact]
    public async Task Backfill_NewSeasonAddedToShow_AddsNewEpisodesToPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, season1Id, season1EpisodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 2);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShow, showId),
            (MediaTypeValues.TVShowSeason, season1Id),
            (MediaTypeValues.TVShowEpisode, season1EpisodeIds[0]),
            (MediaTypeValues.TVShowEpisode, season1EpisodeIds[1]));

        // A new season (with episodes) is added to the media library after the playlist was populated.
        var (season2Id, season2EpisodeIds) = await AddSeasonToShowAsync(showId, "Staffel 2", 2);

        var backfillService = CreateBackfillService();
        var result = await RunPendingBackfillAsync(backfillService, ct);

        Assert.Equal(3, result.EntriesAdded);
        var entryRefs = await _db.PlaylistEntries.AsNoTracking()
            .Where(e => e.PlaylistId == playlistId)
            .Select(e => new { e.MediaType, e.MediaId })
            .ToListAsync(ct);
        Assert.Contains(entryRefs, e => e.MediaType == MediaTypeValues.TVShowSeason && e.MediaId == season2Id);
        Assert.Contains(entryRefs, e => e.MediaType == MediaTypeValues.TVShowEpisode && e.MediaId == season2EpisodeIds[0]);
        Assert.Contains(entryRefs, e => e.MediaType == MediaTypeValues.TVShowEpisode && e.MediaId == season2EpisodeIds[1]);
    }

    [Fact]
    public async Task Backfill_NewMovieAddedToCollection_AddsMovieToPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var existingMovieId = await AddMovieToCollectionAsync(collectionId, "Film 1");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.MovieCollection, collectionId),
            (MediaTypeValues.Movie, existingMovieId));

        var newMovieId = await AddMovieToCollectionAsync(collectionId, "Film 2");

        var backfillService = CreateBackfillService();
        var result = await RunPendingBackfillAsync(backfillService, ct);

        Assert.Equal(1, result.EntriesAdded);
        Assert.True(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == newMovieId, ct));
    }

    [Fact]
    public async Task Backfill_ManualSortMode_AppendsNewEntryAtEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var existingMovieId = await AddMovieToCollectionAsync(collectionId, "Film 1");
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.MovieCollection, collectionId, 0L),
            (MediaTypeValues.Movie, existingMovieId, 1L));

        var newMovieId = await AddMovieToCollectionAsync(collectionId, "Film 2");

        var backfillService = CreateBackfillService();
        await RunPendingBackfillAsync(backfillService, ct);

        var newEntry = await _db.PlaylistEntries.AsNoTracking()
            .SingleAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == newMovieId, ct);
        Assert.Equal(2L, newEntry.SortOrder);
    }

    [Fact]
    public async Task Backfill_PreviouslyExcludedTitle_IsNotReAdded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, seasonId, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 2);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShow, showId),
            (MediaTypeValues.TVShowSeason, seasonId),
            (MediaTypeValues.TVShowEpisode, episodeIds[0]));

        _db.PlaylistEntryExclusions.Add(new PlaylistEntryExclusion
        {
            PlaylistId = playlistId,
            MediaType = MediaTypeValues.TVShowEpisode,
            MediaId = episodeIds[1],
            ExcludedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        var backfillService = CreateBackfillService();
        var result = await RunPendingBackfillAsync(backfillService, ct);

        Assert.Equal(0, result.EntriesAdded);
        Assert.False(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.TVShowEpisode && e.MediaId == episodeIds[1], ct));
    }

    [Fact]
    public async Task Backfill_MaxPlaylistItemCountAlreadyReached_AddsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, seasonId, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShow, showId),
            (MediaTypeValues.TVShowSeason, seasonId),
            (MediaTypeValues.TVShowEpisode, episodeIds[0]));

        var (_, newEpisodeIds) = await AddSeasonToShowAsync(showId, "Staffel 2", 1);

        // The playlist already has 3 entries (show, season 1, episode 1) - at the configured maximum.
        var backfillService = CreateBackfillService(maxPlaylistItemCount: 3);
        var result = await RunPendingBackfillAsync(backfillService, ct);

        Assert.Equal(0, result.EntriesAdded);
        Assert.False(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.TVShowEpisode && e.MediaId == newEpisodeIds[0], ct));
    }

    [Fact]
    public async Task Backfill_MaxPlaylistItemCountPartiallyReached_FillsOnlyUpToLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, seasonId, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShow, showId),
            (MediaTypeValues.TVShowSeason, seasonId),
            (MediaTypeValues.TVShowEpisode, episodeIds[0]));

        // Adding a season with 2 episodes would add 3 entries (season + 2 episodes), but only 1 slot remains.
        await AddSeasonToShowAsync(showId, "Staffel 2", 2);

        var backfillService = CreateBackfillService(maxPlaylistItemCount: 4);
        var result = await RunPendingBackfillAsync(backfillService, ct);

        Assert.Equal(1, result.EntriesAdded);
        Assert.Equal(4, await _db.PlaylistEntries.AsNoTracking().CountAsync(e => e.PlaylistId == playlistId, ct));
    }

    [Fact]
    public async Task Backfill_EveryChildAlreadyPresent_AddsNothingAndDoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, seasonId, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 2);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShow, showId),
            (MediaTypeValues.TVShowSeason, seasonId),
            (MediaTypeValues.TVShowEpisode, episodeIds[0]),
            (MediaTypeValues.TVShowEpisode, episodeIds[1]));

        var backfillService = CreateBackfillService();

        var result = await RunPendingBackfillAsync(backfillService, ct);

        Assert.Equal(0, result.EntriesAdded);
        Assert.Equal(4, await _db.PlaylistEntries.AsNoTracking().CountAsync(e => e.PlaylistId == playlistId, ct));
    }

    [Fact]
    public async Task Backfill_PlaylistWithoutCollectionEntry_IsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var backfillService = CreateBackfillService();
        var result = await RunPendingBackfillAsync(backfillService, ct);

        Assert.Equal(0, result.PlaylistsExamined);
        Assert.Equal(0, result.EntriesAdded);
    }
}
