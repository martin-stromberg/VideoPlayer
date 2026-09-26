using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for the Entwicklungsschritt-7 Sicherheitsabfrage: <see cref="PlaylistService.RemoveMediaFromPlaylistAsync"/>
/// removing a single entry from a playlist while a <see cref="ContinueWatchingEntry"/> bound to that same
/// playlist still references it. Deliberately runs against the real SQLite database
/// <see cref="PlaylistServiceTestBase"/> already uses (via <see cref="PlaylistServiceTestBase.CreatePlaylistServiceWithConflictResolution"/>,
/// wiring a real <see cref="ContinueWatchingService"/>) - like <c>PlaylistServiceTests_DeleteWithConflictResolution</c>,
/// this exercises the unique index on (UserId, MovieId/TVShowEpisodeId, PlaylistId) for the "replace with
/// next available title" collision case, which EF InMemory would not enforce.
/// </summary>
public class PlaylistServiceTests_RemoveMediaWithContinueWatching : PlaylistServiceTestBase
{
    [Fact]
    public async Task RemoveMedia_WithContinueWatchingReference_WithoutConfirmation_ThrowsAndDoesNotRemove()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 1");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        await AddContinueWatchingEntryAsync(movieId, playlistId);

        await Assert.ThrowsAsync<ContinueWatchingConfirmationRequiredException>(
            () => service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, cancellationToken: ct));

        // Weder der Playlist-Eintrag noch der Weiterschauen-Eintrag wurden angetastet.
        Assert.True(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == movieId, ct));
        Assert.True(await _db.ContinueWatchingEntries.AsNoTracking()
            .AnyAsync(e => e.UserId == _testUserId && e.PlaylistId == playlistId && e.MovieId == movieId, ct));
    }

    [Fact]
    public async Task RemoveMedia_WithContinueWatchingReference_Confirmed_ReplacesWithNextAvailableTitle()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var movie1 = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 1");
        var movie2 = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 2");
        var movie3 = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 3");
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movie1, 0),
            (MediaTypeValues.Movie, movie2, 1),
            (MediaTypeValues.Movie, movie3, 2));

        var cwEntry = await AddContinueWatchingEntryAsync(movie2, playlistId, position: TimeSpan.FromMinutes(10));

        await service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movie2, confirmContinueWatchingRemoval: true, cancellationToken: ct);

        Assert.False(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == movie2, ct));

        var updated = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == cwEntry.Id, ct);
        Assert.Equal(movie3, updated.MovieId);
        Assert.Equal(playlistId, updated.PlaylistId);
        Assert.Equal(TimeSpan.Zero, updated.Position);
    }

    [Fact]
    public async Task RemoveMedia_WithContinueWatchingReference_Confirmed_NoNextTitle_RemovesEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();

        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Einziger Titel");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var cwEntry = await AddContinueWatchingEntryAsync(movieId, playlistId);

        await service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, confirmContinueWatchingRemoval: true, cancellationToken: ct);

        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == cwEntry.Id, ct));
    }

    [Fact]
    public async Task RemoveMedia_WithContinueWatchingReference_Confirmed_CollisionWithExistingNextEntry_KeepsExistingAndRemovesReplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();

        var movie1 = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 1");
        var movie2 = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 2");
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movie1, 0),
            (MediaTypeValues.Movie, movie2, 1));

        // Weiterschauen-Eintrag fuer Titel 1 (wird entfernt) UND bereits ein eigener Weiterschauen-Eintrag
        // fuer Titel 2 (das Ziel des Ersetzens) in derselben Playlist - eine Kollision, die den Unique-Index
        // (UserId, MovieId, PlaylistId) verletzen wuerde, wenn beide Eintraege gleichzeitig existierten.
        var replacedEntry = await AddContinueWatchingEntryAsync(movie1, playlistId, position: TimeSpan.FromMinutes(5));
        var existingTargetEntry = await AddContinueWatchingEntryAsync(movie2, playlistId, position: TimeSpan.FromMinutes(20));

        await service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movie1, confirmContinueWatchingRemoval: true, cancellationToken: ct);

        // Der zu ersetzende Eintrag wurde entfernt (nicht auf movie2 umgehaengt), der bereits vorhandene
        // Zieleintrag mit seinem echten Fortschritt bleibt unveraendert bestehen.
        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == replacedEntry.Id, ct));
        var remaining = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == existingTargetEntry.Id, ct);
        Assert.Equal(movie2, remaining.MovieId);
        Assert.Equal(TimeSpan.FromMinutes(20), remaining.Position);
    }

    [Fact]
    public async Task RemoveMedia_ContinueWatchingEntryWithoutPlaylistReference_IsNotTouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();

        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        // Weiterschauen-Eintrag OHNE Playlist-Bezug (PlaylistId = null) fuer dasselbe Video.
        var freeEntry = await AddContinueWatchingEntryAsync(movieId, playlistId: null, position: TimeSpan.FromMinutes(3));

        await service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, cancellationToken: ct);

        Assert.False(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == movieId, ct));

        var stillThere = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == freeEntry.Id, ct);
        Assert.Null(stillThere.PlaylistId);
        Assert.Equal(TimeSpan.FromMinutes(3), stillThere.Position);
    }

    [Fact]
    public async Task RemoveMedia_ContinueWatchingEntryBoundToDifferentPlaylist_IsNotTouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();

        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        var otherPlaylistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var otherPlaylistEntry = await AddContinueWatchingEntryAsync(movieId, otherPlaylistId, position: TimeSpan.FromMinutes(7));

        await service.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, cancellationToken: ct);

        var stillThere = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == otherPlaylistEntry.Id, ct);
        Assert.Equal(otherPlaylistId, stillThere.PlaylistId);
        Assert.Equal(TimeSpan.FromMinutes(7), stillThere.Position);
    }

    /// <summary>
    /// Documents an architectural consequence discovered while implementing the silent orphan-cleanup path
    /// (<c>PlaylistService.ResolveOrphanContinueWatchingReplacementsAsync</c>): <see cref="ContinueWatchingEntryConfiguration"/>
    /// already declares a real foreign key with <c>OnDelete(DeleteBehavior.Cascade)</c> from
    /// <see cref="ContinueWatchingEntry.MovieId"/>/<see cref="ContinueWatchingEntry.TVShowEpisodeId"/> to
    /// <c>Movies</c>/<c>TVShowEpisodes</c> (pre-existing, from before Entwicklungsschritt 7). So the instant
    /// a movie/episode row is actually deleted from the database, SQLite itself immediately deletes any
    /// <see cref="ContinueWatchingEntry"/> still referencing it - well before <c>PlaylistService</c>'s own,
    /// unrelated <c>PlaylistEntry</c> orphan sweep (which only runs lazily on the next playlist load, since
    /// <c>PlaylistEntry</c> has no real FK to the media it references) gets a chance to look for one to
    /// replace. This test verifies that resulting state is exactly what the cascade alone already produces
    /// (the continue-watching entry is gone) and that the silent orphan cleanup does not error or create any
    /// incorrect entry afterwards - see the "Ersetzen"-semantics note about this in the final report.
    /// </summary>
    [Fact]
    public async Task RemoveMedia_OrphanedByLibraryDisappearance_ContinueWatchingEntryAlreadyGoneViaCascade()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var movie1 = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 1");
        var movie2 = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 2");
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movie1, 0),
            (MediaTypeValues.Movie, movie2, 1));

        var cwEntry = await AddContinueWatchingEntryAsync(movie1, playlistId, position: TimeSpan.FromMinutes(2));

        // Titel 1 verschwindet direkt aus dem Medienbestand. Das loescht cwEntry bereits jetzt per
        // Kaskaden-FK (siehe XML-Doc oben) - der PlaylistEntry selbst bleibt zunaechst als Waise stehen,
        // bis er beim naechsten Laden erkannt wird.
        var movie = await _db.Movies.SingleAsync(m => m.Id == movie1, ct);
        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync(ct);

        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == cwEntry.Id, ct));

        // Kein interaktiver, bestaetigungspflichtiger Aufruf noetig - GetPlaylistEntriesAsync loest die
        // stille Waisen-Bereinigung des PlaylistEntry aus; die (bereits per Kaskade entfernte)
        // Weiterschauen-Bereinigung laeuft mit, ohne Fehler und ohne einen neuen Eintrag zu erzeugen.
        var remainingEntries = await service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);
        Assert.Single(remainingEntries);
        Assert.Equal(movie2, remainingEntries[0].MediaId);

        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.PlaylistId == playlistId, ct));
    }

    [Fact]
    public async Task RemoveMedia_OrphanedByLibraryDisappearance_NoNextTitle_SilentlyRemovesContinueWatchingEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();

        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Einziger Titel");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var cwEntry = await AddContinueWatchingEntryAsync(movieId, playlistId);

        var movie = await _db.Movies.SingleAsync(m => m.Id == movieId, ct);
        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync(ct);

        var remainingEntries = await service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);
        Assert.Empty(remainingEntries);

        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == cwEntry.Id, ct));
    }

    /// <summary>
    /// Creates and persists a <see cref="ContinueWatchingEntry"/> for <see cref="PlaylistServiceTestBase._testUserId"/>
    /// referencing the given movie, optionally bound to a playlist.
    /// </summary>
    /// <param name="movieId">The movie id the entry references.</param>
    /// <param name="playlistId">The playlist id to bind the entry to, or <see langword="null"/> for a playlist-less entry.</param>
    /// <param name="position">The playback position to seed, defaulting to one minute.</param>
    /// <returns>The created and persisted entry.</returns>
    private async Task<ContinueWatchingEntry> AddContinueWatchingEntryAsync(long movieId, long? playlistId, TimeSpan? position = null)
    {
        var entry = new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movieId,
            PlaylistId = playlistId,
            Position = position ?? TimeSpan.FromMinutes(1),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = DateTime.UtcNow.Ticks
        };
        _db.ContinueWatchingEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }
}
