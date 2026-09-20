using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Entwicklungsschritt 11: what deleting a public playlist, removing a title from it, clearing its public
/// flag or a viewer's own playback progress do to the continue-watching (Weiterschauen) entries of the
/// OTHER users bound to the playlist. Runs against the real SQLite database (unique indexes and the
/// <c>ON DELETE SET NULL</c> foreign key are exactly what these tests are about; EF InMemory enforces
/// neither). <c>_otherUserId</c> is the owner, <c>_testUserId</c> the (first) viewer.
/// </summary>
public class PlaylistServiceTests_PublicContinueWatching : PlaylistServiceTestBase
{
    private const string SecondViewerId = "viewer-two-789";

    [Fact]
    public async Task Delete_PublicPlaylist_ViewerWithBoundAndFreeEntryForSameVideo_DeletionSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        await EnsureUserAsync(SecondViewerId);
        var movie = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movie));
        await MakePlaylistPublicAsync(playlistId);

        // Viewer 1: playlist-bound AND playlist-less entry for the same video (the UNIQUE trap).
        var viewer1Bound = await AddEntryAsync(_testUserId, movie, playlistId, TimeSpan.FromMinutes(5));
        var viewer1Free = await AddEntryAsync(_testUserId, movie, null, TimeSpan.FromMinutes(2));
        // Viewer 2: only a bound entry - kept, playlist reference cleared by the database.
        var viewer2Bound = await AddEntryAsync(SecondViewerId, movie, playlistId, TimeSpan.FromMinutes(7));
        // Owner's own bound entry.
        var ownerBound = await AddEntryAsync(_otherUserId, movie, playlistId, TimeSpan.FromMinutes(9));

        await service.DeletePlaylistAsync(playlistId, _otherUserId, ct);

        Assert.False(await _db.Playlists.AsNoTracking().AnyAsync(p => p.Id == playlistId, ct));
        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == viewer1Bound.Id, ct));
        var free = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == viewer1Free.Id, ct);
        Assert.Null(free.PlaylistId);
        Assert.Equal(TimeSpan.FromMinutes(2), free.Position);

        var v2 = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == viewer2Bound.Id, ct);
        Assert.Null(v2.PlaylistId);
        Assert.Equal(TimeSpan.FromMinutes(7), v2.Position);

        var owner = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == ownerBound.Id, ct);
        Assert.Null(owner.PlaylistId);
    }

    [Fact]
    public async Task Unpublish_DetachesViewersEntries_ResolvesUniqueConflict_KeepsOwnersEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        await EnsureUserAsync(SecondViewerId);
        var movie = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movie));
        await MakePlaylistPublicAsync(playlistId);

        var viewer1Bound = await AddEntryAsync(_testUserId, movie, playlistId, TimeSpan.FromMinutes(5));
        var viewer1Free = await AddEntryAsync(_testUserId, movie, null, TimeSpan.FromMinutes(2));
        var viewer2Bound = await AddEntryAsync(SecondViewerId, movie, playlistId, TimeSpan.FromMinutes(7));
        var ownerBound = await AddEntryAsync(_otherUserId, movie, playlistId, TimeSpan.FromMinutes(9));

        var dto = await service.SetPlaylistPublicAsync(playlistId, _otherUserId, requesterIsAdmin: true, isPublic: false, ct);

        Assert.False(dto.IsPublic);
        // Viewer 1: the existing playlist-less entry wins, the bound one is gone (no UNIQUE violation).
        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == viewer1Bound.Id, ct));
        Assert.Equal(TimeSpan.FromMinutes(2), (await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == viewer1Free.Id, ct)).Position);
        // Viewer 2: turned into a plain entry, playback position preserved.
        var v2 = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == viewer2Bound.Id, ct);
        Assert.Null(v2.PlaylistId);
        Assert.Equal(TimeSpan.FromMinutes(7), v2.Position);
        // The owner keeps their own playlist context.
        Assert.Equal(playlistId, (await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == ownerBound.Id, ct)).PlaylistId);
        // And nobody but the owner can reach the playlist any more.
        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => service.GetPlaylistAsync(playlistId, _testUserId, ct));
    }

    [Fact]
    public async Task Publish_DoesNotTouchAnyContinueWatchingEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        var movie = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movie));
        var ownerBound = await AddEntryAsync(_otherUserId, movie, playlistId, TimeSpan.FromMinutes(9));

        await service.SetPlaylistPublicAsync(playlistId, _otherUserId, requesterIsAdmin: true, isPublic: true, ct);

        Assert.Equal(playlistId, (await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == ownerBound.Id, ct)).PlaylistId);
    }

    [Fact]
    public async Task ContinueWatchingList_EntryBoundToPlaylistViewerMayNotRead_DoesNotDiscloseNameOrEntry()
    {
        // Defensive layer (unpublish already detaches): even a stale bound entry must never reveal the name
        // of a playlist that is private for its user, nor its entry id (deep link target).
        var ct = TestContext.Current.CancellationToken;
        // (GetListAsync expects movies to belong to a collection.)
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var movie = await AddMovieToCollectionAsync(collectionId, "Film");
        var privatePlaylistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movie));
        var publicMovie = await AddMovieToCollectionAsync(collectionId, "Film 2");
        var publicPlaylistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, publicMovie));
        await MakePlaylistPublicAsync(publicPlaylistId);
        await AddEntryAsync(_testUserId, movie, privatePlaylistId, TimeSpan.FromMinutes(5));
        await AddEntryAsync(_testUserId, publicMovie, publicPlaylistId, TimeSpan.FromMinutes(5));
        var cw = BuildContinueWatchingService(() => _service);

        var list = await cw.GetListAsync(new System.Security.Claims.ClaimsPrincipal(), ct);

        var privateItem = Assert.Single(list, i => i.PlaylistId == privatePlaylistId);
        Assert.Null(privateItem.PlaylistName);
        Assert.Null(privateItem.PlaylistEntryId);
        var publicItem = Assert.Single(list, i => i.PlaylistId == publicPlaylistId);
        Assert.NotNull(publicItem.PlaylistName);
        Assert.NotNull(publicItem.PlaylistEntryId);
    }

    [Fact]
    public async Task RemoveTitle_ViewerOnlyEntry_NeedsNoConfirmation_ReplacedWithNextTitleAccessibleToTheViewer()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, playlistId, m1, _, m3) = await CreatePublicThreeTitlePlaylistAsync();
        var viewerEntry = await AddEntryAsync(_testUserId, m1, playlistId, TimeSpan.FromMinutes(3));

        // The owner has no entry of their own for m1: no Sicherheitsabfrage - and the 409 machinery is not
        // triggered by somebody else's entry (nothing about other users leaks into the owner's dialog).
        await service.RemoveMediaFromPlaylistAsync(playlistId, _otherUserId, MediaTypeValues.Movie, m1, confirmContinueWatchingRemoval: false, ct);

        Assert.False(await _db.PlaylistEntries.AsNoTracking().AnyAsync(e => e.PlaylistId == playlistId && e.MediaId == m1, ct));
        // Viewer may only access m3 (m2 is locked for them): replaced with m3, not with the owner's next title m2.
        var updated = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == viewerEntry.Id, ct);
        Assert.Equal(m3, updated.MovieId);
        Assert.Equal(playlistId, updated.PlaylistId);
        Assert.Equal(TimeSpan.Zero, updated.Position);
    }

    [Fact]
    public async Task RemoveTitle_OwnerAndViewerBothHaveEntries_ConfirmationOnlyForOwnersOwnEntry_ThenEachGetsTheirOwnNextTitle()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, playlistId, m1, m2, m3) = await CreatePublicThreeTitlePlaylistAsync();
        var ownerEntry = await AddEntryAsync(_otherUserId, m1, playlistId, TimeSpan.FromMinutes(4));
        var viewerEntry = await AddEntryAsync(_testUserId, m1, playlistId, TimeSpan.FromMinutes(3));

        await Assert.ThrowsAsync<ContinueWatchingConfirmationRequiredException>(
            () => service.RemoveMediaFromPlaylistAsync(playlistId, _otherUserId, MediaTypeValues.Movie, m1, false, ct));
        Assert.True(await _db.PlaylistEntries.AsNoTracking().AnyAsync(e => e.PlaylistId == playlistId && e.MediaId == m1, ct));
        Assert.Equal(m1, (await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == viewerEntry.Id, ct)).MovieId);

        await service.RemoveMediaFromPlaylistAsync(playlistId, _otherUserId, MediaTypeValues.Movie, m1, true, ct);

        Assert.Equal(m2, (await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == ownerEntry.Id, ct)).MovieId);
        Assert.Equal(m3, (await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == viewerEntry.Id, ct)).MovieId);
    }

    [Fact]
    public async Task RemoveTitle_ViewerEntry_NoFurtherTitleAccessibleToViewer_EntryIsRemoved()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, playlistId, _, _, m3) = await CreatePublicThreeTitlePlaylistAsync();
        var viewerEntry = await AddEntryAsync(_testUserId, m3, playlistId, TimeSpan.FromMinutes(3));

        // m3 is last in order: nothing follows, so the viewer's entry disappears.
        await service.RemoveMediaFromPlaylistAsync(playlistId, _otherUserId, MediaTypeValues.Movie, m3, false, ct);

        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == viewerEntry.Id, ct));
    }

    [Fact]
    public async Task RemoveTitle_ViewerAlreadyHasEntryForTheNextTitle_KeepsThatEntry_NoUniqueViolation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, playlistId, m1, _, m3) = await CreatePublicThreeTitlePlaylistAsync();
        var viewerOnM1 = await AddEntryAsync(_testUserId, m1, playlistId, TimeSpan.FromMinutes(3));
        var viewerOnM3 = await AddEntryAsync(_testUserId, m3, playlistId, TimeSpan.FromMinutes(20));

        await service.RemoveMediaFromPlaylistAsync(playlistId, _otherUserId, MediaTypeValues.Movie, m1, false, ct);

        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == viewerOnM1.Id, ct));
        Assert.Equal(TimeSpan.FromMinutes(20), (await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == viewerOnM3.Id, ct)).Position);
    }

    [Fact]
    public async Task RemoveTitle_ViewersEntriesForOtherTitlesAndPlaylistlessEntries_StayUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, playlistId, m1, m2, _) = await CreatePublicThreeTitlePlaylistAsync();
        var otherTitle = await AddEntryAsync(_testUserId, m2, playlistId, TimeSpan.FromMinutes(6));
        var playlistless = await AddEntryAsync(_testUserId, m1, null, TimeSpan.FromMinutes(8));

        await service.RemoveMediaFromPlaylistAsync(playlistId, _otherUserId, MediaTypeValues.Movie, m1, false, ct);

        Assert.Equal(TimeSpan.FromMinutes(6), (await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == otherTitle.Id, ct)).Position);
        var free = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == playlistless.Id, ct);
        Assert.Equal(m1, free.MovieId);
        Assert.Null(free.PlaylistId);
    }

    [Fact]
    public async Task DeleteMediaSource_ViewersEntry_ReplacedWithNextTitleTheViewerMayAccess_OtherwiseRemoved()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        await EnsureUserAsync(SecondViewerId);
        await GrantMediaSourceAccessForUserAsync(_testUserId, mediaSourceId: 1);
        await GrantMediaSourceAccessForUserAsync(_testUserId, mediaSourceId: 2); // viewer 1 may see source 2
        // Viewer 2 has access to neither source afterwards -> nothing to fall back to.

        var movieA = new Movie { Name = "Titel A (Quelle 1)", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        var movieB = new Movie { Name = "Titel B (Quelle 2)", MediaSourceId = 2, CreatedAt = DateTime.UtcNow };
        _db.Movies.AddRange(movieA, movieB);
        await _db.SaveChangesAsync(ct);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_otherUserId,
            (MediaTypeValues.Movie, movieA.Id, 0),
            (MediaTypeValues.Movie, movieB.Id, 1));
        await MakePlaylistPublicAsync(playlistId);
        var viewer1 = await AddEntryAsync(_testUserId, movieA.Id, playlistId, TimeSpan.FromMinutes(4));
        var viewer2 = await AddEntryAsync(SecondViewerId, movieA.Id, playlistId, TimeSpan.FromMinutes(4));

        var source1 = await _db.MediaSources.SingleAsync(s => s.Id == 1, ct);
        await _db.DeleteMediaSourceAsync(source1, null, ct,
            beforeContinueWatchingCleanupAsync: token => service.ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(1, token));

        var replaced = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == viewer1.Id, ct);
        Assert.Equal(movieB.Id, replaced.MovieId);
        Assert.Equal(playlistId, replaced.PlaylistId);
        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(e => e.Id == viewer2.Id, ct));
    }

    [Fact]
    public async Task ViewerProgress_OnPublicPlaylist_LandsInViewersOwnList_WithoutTouchingPlaylistOrOthersProgress()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        var cw = BuildContinueWatchingService(() => service);
        var movie = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movie));
        await MakePlaylistPublicAsync(playlistId);
        var ownerEntry = await AddEntryAsync(_otherUserId, movie, playlistId, TimeSpan.FromMinutes(9));
        var playlistBefore = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct);
        var entryCountBefore = await _db.PlaylistEntries.AsNoTracking().CountAsync(e => e.PlaylistId == playlistId, ct);

        // Viewer (_testUserId) reports progress for the public playlist: validation passes ...
        await cw.ValidatePlaylistAccessAsync(_testUserId, playlistId, ct);
        // ... and the buffered entry is processed like for any playlist playback.
        await cw.ProcessBufferedEntryAsync(_testUserId, movie, null, TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(90), playlistId, ct);

        var viewerEntry = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.UserId == _testUserId, ct);
        Assert.Equal(playlistId, viewerEntry.PlaylistId);
        Assert.Equal(movie, viewerEntry.MovieId);
        Assert.Equal(TimeSpan.FromMinutes(12), viewerEntry.Position);

        // Owner's progress and the playlist itself are unchanged.
        Assert.Equal(TimeSpan.FromMinutes(9), (await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == ownerEntry.Id, ct)).Position);
        var playlistAfter = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct);
        Assert.Equal(playlistBefore.UpdatedAt, playlistAfter.UpdatedAt);
        Assert.Equal(playlistBefore.Name, playlistAfter.Name);
        Assert.Equal(entryCountBefore, await _db.PlaylistEntries.AsNoTracking().CountAsync(e => e.PlaylistId == playlistId, ct));
    }

    [Fact]
    public async Task ViewerProgress_OnPrivatePlaylist_IsRefused_AndAfterUnpublish()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreatePlaylistServiceWithConflictResolution();
        var cw = BuildContinueWatchingService(() => service);
        var movie = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movie));

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => cw.ValidatePlaylistAccessAsync(_testUserId, playlistId, ct));
        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => cw.ProcessBufferedEntryAsync(_testUserId, movie, null, TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(90), playlistId, ct));

        await service.SetPlaylistPublicAsync(playlistId, _otherUserId, requesterIsAdmin: true, isPublic: true, ct);
        await cw.ValidatePlaylistAccessAsync(_testUserId, playlistId, ct);
        await service.SetPlaylistPublicAsync(playlistId, _otherUserId, requesterIsAdmin: true, isPublic: false, ct);
        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => cw.ValidatePlaylistAccessAsync(_testUserId, playlistId, ct));
    }

    [Fact]
    public async Task DeleteOwnerAccount_ViewersEntriesBoundToTwoOfTheirPublicPlaylists_DoesNotViolateUniqueIndex()
    {
        // Two public playlists of the same owner, both containing the same movie, and a viewer with a bound
        // entry for it in each: once the owner's account (and via cascade both playlists) is deleted, both
        // entries would become playlist-less at once. Resolved up front, as UserManagement.DeleteUser does.
        var ct = TestContext.Current.CancellationToken;
        var cw = BuildContinueWatchingService(() => _service);
        var movie = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film");
        var playlist1 = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movie));
        var playlist2 = await CreateTestPlaylistWithEntriesAsync(_otherUserId, (MediaTypeValues.Movie, movie));
        await MakePlaylistPublicAsync(playlist1);
        await MakePlaylistPublicAsync(playlist2);
        await AddEntryAsync(_testUserId, movie, playlist1, TimeSpan.FromMinutes(5));
        await AddEntryAsync(_testUserId, movie, playlist2, TimeSpan.FromMinutes(6));

        await cw.ResolveDeletionConflictsForOwnedPlaylistsAsync(_otherUserId, ct);
        _db.Users.Remove(await _db.Users.SingleAsync(u => u.Id == _otherUserId, ct));
        await _db.SaveChangesAsync(ct);

        var remaining = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.UserId == _testUserId, ct);
        Assert.Null(remaining.PlaylistId);
        Assert.Equal(movie, remaining.MovieId);
    }

    /// <summary>
    /// A public playlist of the owner (<c>_otherUserId</c>, regular source access - everything accessible)
    /// with three movies in manual order m1, m2, m3, where the viewer (<c>_testUserId</c>) can only access
    /// m3 (through an unlocked movie collection) - so "next title accessible to the viewer" after m1 is m3,
    /// while for the owner it is m2.
    /// </summary>
    /// <returns>The wired playlist service, the playlist id and the three movie ids.</returns>
    private async Task<(PlaylistService Service, long PlaylistId, long M1, long M2, long M3)> CreatePublicThreeTitlePlaylistAsync()
    {
        var service = CreatePlaylistServiceWithConflictResolution();
        await GrantMediaSourceAccessForUserAsync(_otherUserId);
        var m1 = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 1");
        var m2 = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Titel 2");
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var m3 = await AddMovieToCollectionAsync(collectionId, "Titel 3");
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.MovieCollection, collectionId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_otherUserId,
            (MediaTypeValues.Movie, m1, 0),
            (MediaTypeValues.Movie, m2, 1),
            (MediaTypeValues.Movie, m3, 2));
        await MakePlaylistPublicAsync(playlistId);
        return (service, playlistId, m1, m2, m3);
    }

    /// <summary>Adds a further user row (needed as continue-watching entries reference an existing user).</summary>
    /// <param name="userId">The id of the user to create.</param>
    private async Task EnsureUserAsync(string userId)
    {
        _db.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}@test.com" });
        await _db.SaveChangesAsync();
    }

    /// <summary>Creates and persists a continue-watching entry for the given user and movie.</summary>
    /// <param name="userId">The owning user of the entry.</param>
    /// <param name="movieId">The movie the entry references.</param>
    /// <param name="playlistId">The playlist to bind it to, or <see langword="null"/> for a plain entry.</param>
    /// <param name="position">The playback position.</param>
    /// <returns>The persisted entry.</returns>
    private async Task<ContinueWatchingEntry> AddEntryAsync(string userId, long movieId, long? playlistId, TimeSpan position)
    {
        var entry = new ContinueWatchingEntry
        {
            UserId = userId,
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
