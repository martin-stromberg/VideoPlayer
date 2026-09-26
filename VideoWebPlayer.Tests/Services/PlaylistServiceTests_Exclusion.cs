using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for the "deliberately removed titles are not re-added" mechanism (Entwicklungsschritt 8):
/// <see cref="VideoWebPlayer.Services.PlaylistService.RemoveMediaFromPlaylistAsync"/> records a
/// <see cref="PlaylistEntryExclusion"/>, and <see cref="VideoWebPlayer.Services.PlaylistService.AddMediaToPlaylistAsync"/>
/// lifts it again for whatever it (re-)adds. The consuming side (the automatic backfill mechanism actually
/// skipping excluded titles) is covered by <c>PlaylistBackfillServiceTests</c>.
/// </summary>
public class PlaylistServiceTests_Exclusion : PlaylistServiceTestBase
{
    [Fact]
    public async Task RemoveMedia_RemovesEntry_RecordsExclusion()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        await _service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, cancellationToken: ct);

        var exclusion = await _db.PlaylistEntryExclusions.AsNoTracking().SingleAsync(
            x => x.PlaylistId == playlistId && x.MediaType == MediaTypeValues.Movie && x.MediaId == movieId, ct);
        Assert.NotNull(exclusion);
    }

    [Fact]
    public async Task AddMedia_ReAddingPreviouslyRemovedSingleTitle_ClearsExclusion()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        await _service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, cancellationToken: ct);
        Assert.True(await _db.PlaylistEntryExclusions.AsNoTracking()
            .AnyAsync(x => x.PlaylistId == playlistId && x.MediaId == movieId, ct));

        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, cancellationToken: ct);

        Assert.False(await _db.PlaylistEntryExclusions.AsNoTracking()
            .AnyAsync(x => x.PlaylistId == playlistId && x.MediaId == movieId, ct));
        Assert.True(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == movieId, ct));
    }

    [Fact]
    public async Task AddMedia_ReAddingWholeShowAfterRemovingOneEpisode_ClearsExclusionAndReAddsEpisode()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, _, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 2);

        // Simulate the normal manual-add cascade outcome: the show plus every season/episode already present.
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));
        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, showId, cancellationToken: ct);

        await _service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShowEpisode, episodeIds[0], cancellationToken: ct);
        Assert.True(await _db.PlaylistEntryExclusions.AsNoTracking()
            .AnyAsync(x => x.PlaylistId == playlistId && x.MediaType == MediaTypeValues.TVShowEpisode && x.MediaId == episodeIds[0], ct));

        // Re-adding the whole show is an explicit inclusion decision that supersedes the earlier removal.
        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, showId, cancellationToken: ct);

        Assert.True(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.TVShowEpisode && e.MediaId == episodeIds[0], ct));
        Assert.False(await _db.PlaylistEntryExclusions.AsNoTracking()
            .AnyAsync(x => x.PlaylistId == playlistId && x.MediaType == MediaTypeValues.TVShowEpisode && x.MediaId == episodeIds[0], ct));
    }

    [Fact]
    public async Task DeletePlaylist_CascadesExclusionRemoval()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        await _service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, cancellationToken: ct);

        await _service.DeletePlaylistAsync(playlistId, _testUserId, cancellationToken: ct);

        Assert.False(await _db.PlaylistEntryExclusions.AsNoTracking().AnyAsync(x => x.PlaylistId == playlistId, ct));
    }
}
