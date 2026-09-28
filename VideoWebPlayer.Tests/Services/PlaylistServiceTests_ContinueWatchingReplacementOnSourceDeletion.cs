using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Regression tests for the Nachbesserung to Entwicklungsschritt 7's "silent" replace-or-remove path
/// (<see cref="IPlaylistService.ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync"/>):
/// the independent acceptance review found that <see cref="ApplicationDbContext.DeleteMediaSourceAsync"/> -
/// the only production code path that actually deletes a movie or TV show episode - unconditionally deleted
/// every affected <see cref="ContinueWatchingEntry"/>, including playlist-bound ones, before the silent
/// replace-with-next-available-title logic ever got a chance to run. These tests exercise the real
/// <see cref="ApplicationDbContext.DeleteMediaSourceAsync"/> together with the fix (a
/// <c>beforeContinueWatchingCleanupAsync</c> hook invoked inside its own transaction) against the real
/// SQLite database <see cref="PlaylistServiceTestBase"/> already uses, mirroring how
/// <c>AdminSourcesController.DeleteSource</c> wires the two together in production.
/// </summary>
public class PlaylistServiceTests_ContinueWatchingReplacementOnSourceDeletion : PlaylistServiceTestBase
{
    [Fact]
    public async Task DeleteMediaSource_PlaylistHasTitleFromOtherSource_ContinueWatchingEntryReplacedWithIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        await GrantMediaSourceAccessForUserAsync(_testUserId, mediaSourceId: 1);
        await GrantMediaSourceAccessForUserAsync(_testUserId, mediaSourceId: 2);

        // Titel A gehoert zur Quelle, die gleich komplett geloescht wird; Titel B gehoert zu einer
        // anderen, weiterhin existierenden Quelle. Beide stehen in derselben Playlist.
        var movieA = new Movie { Name = "Titel A", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        var movieB = new Movie { Name = "Titel B", MediaSourceId = 2, CreatedAt = DateTime.UtcNow };
        _db.Movies.AddRange(movieA, movieB);
        await _db.SaveChangesAsync(ct);

        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movieA.Id, 0),
            (MediaTypeValues.Movie, movieB.Id, 1));

        var cwEntry = await AddContinueWatchingEntryAsync(movieA.Id, playlistId, position: TimeSpan.FromMinutes(4));

        var source1 = await _db.MediaSources.SingleAsync(s => s.Id == 1, ct);
        await _db.DeleteMediaSourceAsync(source1, null, ct,
            beforeContinueWatchingCleanupAsync: token =>
                service.ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(1, token));

        // Titel A ist tatsaechlich geloescht (Quelle 1 wurde geloescht), Titel B (Quelle 2) existiert weiterhin.
        Assert.False(await _db.Movies.AsNoTracking().AnyAsync(m => m.Id == movieA.Id, ct));
        Assert.True(await _db.Movies.AsNoTracking().AnyAsync(m => m.Id == movieB.Id, ct));

        // Der Weiterschauen-Eintrag wurde nicht geloescht, sondern auf Titel B umgehaengt (Position zurueckgesetzt).
        var updated = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == cwEntry.Id, ct);
        Assert.Equal(movieB.Id, updated.MovieId);
        Assert.Equal(playlistId, updated.PlaylistId);
        Assert.Equal(TimeSpan.Zero, updated.Position);
    }

    [Fact]
    public async Task DeleteMediaSource_PlaylistOnlyHasTitlesFromDeletedSource_ContinueWatchingEntryIsRemoved()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        // Legt die MediaSource mit Id 1 an, damit DeleteMediaSourceAsync sie tatsaechlich findet und loescht.
        await GrantMediaSourceAccessForUserAsync(_testUserId, mediaSourceId: 1);

        var movieA = new Movie { Name = "Einziger Titel", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movieA);
        await _db.SaveChangesAsync(ct);

        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieA.Id));

        var cwEntry = await AddContinueWatchingEntryAsync(movieA.Id, playlistId, position: TimeSpan.FromMinutes(2));

        var source1 = await _db.MediaSources.SingleAsync(s => s.Id == 1, ct);
        await _db.DeleteMediaSourceAsync(source1, null, ct,
            beforeContinueWatchingCleanupAsync: token =>
                service.ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(1, token));

        Assert.False(await _db.Movies.AsNoTracking().AnyAsync(m => m.Id == movieA.Id, ct));

        // Kein Ersatztitel verfuegbar (Playlist enthielt nur Titel aus der geloeschten Quelle) - der
        // Weiterschauen-Eintrag wird entfernt statt umgehaengt.
        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == cwEntry.Id, ct));
    }

    /// <summary>
    /// Creates and persists a <see cref="ContinueWatchingEntry"/> for <see cref="PlaylistServiceTestBase._testUserId"/>
    /// referencing the given movie, bound to a playlist. Kept local (rather than reusing the private helper
    /// of the same name in <c>PlaylistServiceTests_RemoveMediaWithContinueWatching</c>) because that helper
    /// is <c>private</c> to its own test class.
    /// </summary>
    /// <param name="movieId">The movie id the entry references.</param>
    /// <param name="playlistId">The playlist id to bind the entry to.</param>
    /// <param name="position">The playback position to seed.</param>
    /// <returns>The created and persisted entry.</returns>
    private async Task<ContinueWatchingEntry> AddContinueWatchingEntryAsync(long movieId, long playlistId, TimeSpan position)
    {
        var entry = new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movieId,
            PlaylistId = playlistId,
            Position = position,
            UpdatedAt = DateTime.UtcNow,
            ListOrder = DateTime.UtcNow.Ticks
        };
        _db.ContinueWatchingEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }
}
