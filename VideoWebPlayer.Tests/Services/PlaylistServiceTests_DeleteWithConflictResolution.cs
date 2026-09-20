using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Regression tests for the "playlist deletion collides with a competing playlist-less continue-watching
/// entry" bug (Weiterschauen mit Playlist-Bezug, Schritt 6 Nachbesserung, Problem 3):
/// <see cref="PlaylistService.DeletePlaylistAsync"/> used to rely blindly on the database's
/// <c>ON DELETE SET NULL</c> foreign-key action when a playlist is deleted, which fails with a UNIQUE
/// constraint violation once a playlist-less <see cref="ContinueWatchingEntry"/> for the same media
/// already exists (both rows would end up with <c>PlaylistId = NULL</c>). Deliberately runs against the
/// real SQLite database <see cref="PlaylistServiceTestBase"/> already uses (unlike
/// <c>ContinueWatchingServiceTestBase</c>'s EF-InMemory provider, which enforces neither unique indexes
/// nor foreign-key actions and therefore cannot reproduce this failure) so the UNIQUE constraint violation
/// - and its fix - are both actually exercised.
/// </summary>
public class PlaylistServiceTests_DeleteWithConflictResolution : PlaylistServiceTestBase
{
    [Fact]
    public async Task DeletePlaylist_WithCompetingNullPlaylistEntry_ResolvesConflictAndDeletesPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film 42");
        var service = CreatePlaylistServiceWithConflictResolution();
        var playlist = await service.CreatePlaylistAsync(_testUserId, "Zu Löschen", null, null, ct);

        var boundEntry = new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movieId,
            PlaylistId = playlist.Id,
            Position = TimeSpan.FromSeconds(300),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        };
        var freeEntry = new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movieId,
            PlaylistId = null,
            Position = TimeSpan.FromSeconds(100),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 2
        };
        _db.ContinueWatchingEntries.AddRange(boundEntry, freeEntry);
        await _db.SaveChangesAsync(ct);

        // Vor der Behebung schlaegt dieser Aufruf mit einer DbUpdateException ("UNIQUE constraint failed")
        // fehl, weil die Datenbank beim Loeschen der Playlist versucht, boundEntry.PlaylistId per
        // ON DELETE SET NULL auf NULL zu setzen, was mit freeEntry kollidiert.
        await service.DeletePlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.False(await _db.Playlists.AnyAsync(p => p.Id == playlist.Id, ct));

        // Der playlist-gebundene Eintrag wurde entfernt (Konfliktaufloesung), nicht auf NULL gesetzt.
        Assert.False(await _db.ContinueWatchingEntries.AnyAsync(e => e.Id == boundEntry.Id, ct));

        // Der urspruenglich playlist-lose Eintrag bleibt unveraendert.
        var remaining = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == freeEntry.Id, ct);
        Assert.Null(remaining.PlaylistId);
        Assert.Equal(TimeSpan.FromSeconds(100), remaining.Position);
    }

    [Fact]
    public async Task DeletePlaylist_WithoutCompetingNullPlaylistEntry_KeepsEntryToBeSetNullByDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film 43");
        var service = CreatePlaylistServiceWithConflictResolution();
        var playlist = await service.CreatePlaylistAsync(_testUserId, "Zu Löschen Ohne Konflikt", null, null, ct);

        var boundEntry = new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movieId,
            PlaylistId = playlist.Id,
            Position = TimeSpan.FromSeconds(300),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        };
        _db.ContinueWatchingEntries.Add(boundEntry);
        await _db.SaveChangesAsync(ct);

        await service.DeletePlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.False(await _db.Playlists.AnyAsync(p => p.Id == playlist.Id, ct));

        var remaining = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == boundEntry.Id, ct);
        Assert.Null(remaining.PlaylistId);
        Assert.Equal(TimeSpan.FromSeconds(300), remaining.Position);
    }
}
