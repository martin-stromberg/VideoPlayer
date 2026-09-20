using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Entwicklungsschritt 11: what a VIEWER (a user who is not the owner) sees and may play on a public
/// playlist - accessibility is always resolved for the viewer, never for the owner (regression tests
/// against confusing the owner's and the viewer's id, and their unlock id spaces) - and that a viewer's
/// read access never modifies the owner's playlist (orphan cleanup stays an owner-only side effect).
/// <c>_testUserId</c> is the viewer, <c>_otherUserId</c> the owner.
/// </summary>
public class PlaylistServiceTests_PublicViewerPlayback : PlaylistServiceTestBase
{
    [Fact]
    public async Task Viewer_WithoutUnlock_SeesOwnersUnlockedTitleAsLocked_OwnerSeesItAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var movieId = await AddMovieToCollectionAsync(collectionId, "Nur fuer den Besitzer");
        // ONLY the owner has the collection unlocked.
        await UnlockMediaForUserAsync(_otherUserId, MediaTypeValues.MovieCollection, collectionId);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movieId));
        await MakePlaylistPublicAsync(playlistId);

        var viewerEntry = Assert.Single((await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 10, ct)).Entries);
        var ownerEntry = Assert.Single((await _service.GetPlaylistEntriesPagedAsync(playlistId, _otherUserId, 1, 10, ct)).Entries);

        Assert.False(viewerEntry.IsAccessible);
        Assert.True(ownerEntry.IsAccessible);
    }

    [Fact]
    public async Task Viewer_CannotPlayTitleOnlyTheOwnerHasUnlocked()
    {
        var ct = TestContext.Current.CancellationToken;
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var movieId = await AddMovieToCollectionAsync(collectionId, "Gesperrt fuer Betrachter");
        await UnlockMediaForUserAsync(_otherUserId, MediaTypeValues.MovieCollection, collectionId);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movieId));
        await MakePlaylistPublicAsync(playlistId);
        var entryId = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).Select(e => e.Id).SingleAsync(ct);

        // Explicit start at the locked title: 403. Implicit start: nothing playable for the viewer.
        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => _service.StartPlaylistAsync(playlistId, _testUserId, entryId, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartPlaylistAsync(playlistId, _testUserId, null, ct));

        // The owner can play it.
        var ownerStart = await _service.StartPlaylistAsync(playlistId, _otherUserId, entryId, ct);
        Assert.Equal(movieId, ownerStart.MediaId);
    }

    [Fact]
    public async Task Viewer_UnlockInOtherIdSpace_DoesNotUnlockTitleOfSameNumericId()
    {
        // Movie collections and TV shows are separate id spaces that both start at 1: the viewer having TV
        // show N unlocked must not make a movie of movie collection N accessible (and vice versa).
        var ct = TestContext.Current.CancellationToken;
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Serie");
        Assert.Equal(collectionId, showId);
        var movieId = await AddMovieToCollectionAsync(collectionId, "Film in Sammlung");
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, showId);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movieId));
        await MakePlaylistPublicAsync(playlistId);

        var entry = Assert.Single((await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 10, ct)).Entries);

        Assert.False(entry.IsAccessible);
    }

    [Fact]
    public async Task Viewer_Navigation_SkipsTitlesLockedForTheViewer_WhileOwnerGetsThem()
    {
        var ct = TestContext.Current.CancellationToken;
        // Owner has regular access to the media source (everything accessible); the viewer only has the
        // collection of the LAST title unlocked.
        await GrantMediaSourceAccessForUserAsync(_otherUserId);
        var first = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Erster");
        var second = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Zweiter (fuer Betrachter gesperrt)");
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var third = await AddMovieToCollectionAsync(collectionId, "Dritter");
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.MovieCollection, collectionId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_otherUserId,
            (MediaTypeValues.Movie, first, 0),
            (MediaTypeValues.Movie, second, 1),
            (MediaTypeValues.Movie, third, 2));
        await MakePlaylistPublicAsync(playlistId);
        var entryIds = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).OrderBy(e => e.SortOrder).Select(e => e.Id).ToListAsync(ct);

        var viewerEntries = (await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 10, ct)).Entries;
        Assert.Equal(new[] { false, false, true }, viewerEntries.Select(e => e.IsAccessible).ToArray());

        var viewerNext = await _service.GetNextPlaylistEntryAsync(playlistId, _testUserId, entryIds[0], ct);
        var ownerNext = await _service.GetNextPlaylistEntryAsync(playlistId, _otherUserId, entryIds[0], ct);

        Assert.Equal(entryIds[2], viewerNext!.Entry.Id);
        Assert.Equal(entryIds[1], ownerNext!.Entry.Id);
    }

    [Fact]
    public async Task ViewerRead_DoesNotRemoveOrphanedEntriesOfTheOwnersPlaylist_OwnerReadDoes()
    {
        // Entwicklungsschritt 11 decision: the orphan sweep (with its genre recomputation and continue-
        // watching replacements) is an owner-only side effect. A read-only viewer just does not see orphans.
        var ct = TestContext.Current.CancellationToken;
        var validMovie = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Vorhanden");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId,
            (MediaTypeValues.Movie, validMovie),
            (MediaTypeValues.Movie, 987_654)); // no such movie: orphan
        await MakePlaylistPublicAsync(playlistId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var updatedAtBefore = (await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct)).UpdatedAt;

        // Viewer: every reading path.
        var paged = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 10, ct);
        var all = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);
        await _service.StartPlaylistAsync(playlistId, _testUserId, null, ct);

        Assert.Equal(validMovie, Assert.Single(paged.Entries).MediaId);
        Assert.Equal(1, paged.TotalCount);
        Assert.Single(all);
        Assert.Equal(2, await _db.PlaylistEntries.AsNoTracking().CountAsync(e => e.PlaylistId == playlistId, ct));
        Assert.Equal(updatedAtBefore, (await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct)).UpdatedAt);

        // Owner: the sweep happens as before.
        await _service.GetPlaylistEntriesPagedAsync(playlistId, _otherUserId, 1, 10, ct);
        Assert.Equal(1, await _db.PlaylistEntries.AsNoTracking().CountAsync(e => e.PlaylistId == playlistId, ct));
    }
}
