using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests that the automatic playlist backfill mechanism (<see cref="VideoWebPlayer.Services.PlaylistBackfillService"/>,
/// Entwicklungsschritt 8) also recomputes a playlist's automatically derived genres
/// (Entwicklungsschritt 9) when it adds newly available cascade children - unless the owner has manually
/// overridden the playlist's genres, in which case a backfill run must not touch them.
/// </summary>
public class PlaylistBackfillServiceTests_Genres : PlaylistServiceTestBase
{
    [Fact]
    public async Task Backfill_NewMovieAddedToCollection_RecomputesPlaylistGenres()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.MovieCollections.Add(collection);
        await _db.SaveChangesAsync(ct);
        await CreateMovieInCollectionWithGenresAsync(collection.Id, "Film 1", "Action");

        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Sammlung-Playlist", null, null, ct);
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.MovieCollection, collection.Id, ct);

        // Baseline genres for the collection-and-its-current-movie playlist before the new movie appears.
        var beforeBackfill = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);
        Assert.NotNull(beforeBackfill);
        Assert.Contains(beforeBackfill!.Genres, g => g.Name == "Action");
        Assert.DoesNotContain(beforeBackfill.Genres, g => g.Name == "Fantasy");

        // A new movie with a new genre appears in the media library after the playlist was populated.
        await CreateMovieInCollectionWithGenresAsync(collection.Id, "Film 2", "Fantasy");

        var backfillService = CreateBackfillService();
        var result = await RunPendingBackfillAsync(backfillService, ct);
        Assert.Equal(1, result.EntriesAdded);

        var afterBackfill = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);
        Assert.NotNull(afterBackfill);
        Assert.Contains(afterBackfill!.Genres, g => g.Name == "Action");
        Assert.Contains(afterBackfill.Genres, g => g.Name == "Fantasy");
    }

    [Fact]
    public async Task Backfill_GenresManuallyOverridden_DoesNotChangeGenresOnBackfill()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.MovieCollections.Add(collection);
        await _db.SaveChangesAsync(ct);
        await CreateMovieInCollectionWithGenresAsync(collection.Id, "Film 1", "Action");

        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Sammlung-Playlist", null, null, ct);
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.MovieCollection, collection.Id, ct);
        var playlistId = playlist.Id;

        var manualGenreId = await GetOrCreateGenreIdAsync("Handverlesen");
        await _service.SetPlaylistGenresAsync(playlistId, _testUserId, new[] { manualGenreId }, ct);

        await CreateMovieInCollectionWithGenresAsync(collection.Id, "Film 2", "Fantasy");

        var backfillService = CreateBackfillService();
        await RunPendingBackfillAsync(backfillService, ct);

        var afterBackfill = await _service.GetPlaylistAsync(playlistId, _testUserId, ct);
        Assert.NotNull(afterBackfill);
        Assert.True(afterBackfill!.GenresManuallyOverridden);
        var single = Assert.Single(afterBackfill.Genres);
        Assert.Equal("Handverlesen", single.Name);
    }
}
