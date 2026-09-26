using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Access matrix of Entwicklungsschritt 11 (oeffentliche Playlists) at service level, against real SQLite:
/// (owner / foreign regular user / administrator who is not the owner) x (every reading and every writing
/// operation of <see cref="IPlaylistService"/>) x (playlist private / public). Reading is allowed for the
/// owner and - only while the playlist is public - for everybody; every mutation stays reserved for the
/// owner, enforced by the service itself (not by hiding UI).
/// </summary>
public class PlaylistServiceTests_PublicAccessMatrix : PlaylistServiceTestBase
{
    /// <summary>A service operation performed as <c>userId</c> on <c>playlistId</c>; <c>entryId</c>/<c>movieId</c> identify an existing entry.</summary>
    /// <param name="service">The service under test.</param>
    /// <param name="playlistId">The playlist to operate on.</param>
    /// <param name="userId">The requesting user.</param>
    /// <param name="entryId">The id of an existing entry of the playlist.</param>
    /// <param name="movieId">The media id of that entry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation's task.</returns>
    public delegate Task Operation(PlaylistService service, long playlistId, string userId, long entryId, long movieId, CancellationToken ct);

    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    /// <summary>Every read operation: allowed for the owner, and for others only on a public playlist.</summary>
    /// <returns>Theory data: operation name and delegate.</returns>
    public static IEnumerable<object[]> ReadOperations() => new[]
    {
        Op("GetPlaylist", (s, p, u, e, m, ct) => s.GetPlaylistAsync(p, u, ct)),
        Op("GetEntries", (s, p, u, e, m, ct) => s.GetPlaylistEntriesAsync(p, u, ct)),
        Op("GetEntriesPaged", (s, p, u, e, m, ct) => s.GetPlaylistEntriesPagedAsync(p, u, 1, 10, ct)),
        Op("StartPlaylist", (s, p, u, e, m, ct) => s.StartPlaylistAsync(p, u, null, ct)),
        Op("StartPlaylistAtEntry", (s, p, u, e, m, ct) => s.StartPlaylistAsync(p, u, e, ct)),
        Op("Next", (s, p, u, e, m, ct) => s.GetNextPlaylistEntryAsync(p, u, e, ct)),
        Op("Previous", (s, p, u, e, m, ct) => s.GetPreviousPlaylistEntryAsync(p, u, e, ct)),
        Op("Advance", (s, p, u, e, m, ct) => s.AdvancePlaylistAsync(p, u, e, ct)),
        Op("GetCover", (s, p, u, e, m, ct) => s.GetPlaylistCoverAsync(p, u, ct)),
    };

    /// <summary>Every write operation: owner only.</summary>
    /// <returns>Theory data: operation name and delegate.</returns>
    public static IEnumerable<object[]> WriteOperations() => new[]
    {
        Op("Update", (s, p, u, e, m, ct) => s.UpdatePlaylistAsync(p, u, "Umbenannt", "x", null, ct)),
        Op("AddMedia", (s, p, u, e, m, ct) => s.AddMediaToPlaylistAsync(p, u, MediaTypeValues.Movie, m, ct)),
        Op("RemoveMedia", (s, p, u, e, m, ct) => s.RemoveMediaFromPlaylistAsync(p, u, MediaTypeValues.Movie, m, false, ct)),
        Op("RemoveMediaConfirmed", (s, p, u, e, m, ct) => s.RemoveMediaFromPlaylistAsync(p, u, MediaTypeValues.Movie, m, true, ct)),
        Op("ReorderEntry", (s, p, u, e, m, ct) => s.ReorderPlaylistEntryAsync(p, u, e, 5, ct)),
        Op("BatchReorder", (s, p, u, e, m, ct) => s.BatchReorderPlaylistEntriesAsync(p, u, new List<(long, long)> { (e, 5) }, ct)),
        Op("GetMaxSortOrder", (s, p, u, e, m, ct) => s.GetMaxSortOrderAsync(p, u, ct)),
        Op("MoveToBeginning", (s, p, u, e, m, ct) => s.MoveEntryToBeginningAsync(p, u, e, ct)),
        Op("MoveBetween", (s, p, u, e, m, ct) => s.MoveEntryBetweenAsync(p, u, e, 3, ct)),
        Op("ChangeSortMode", (s, p, u, e, m, ct) => s.ChangeSortModeAsync(p, u, PlaylistSortModeValues.Manual, null, ct)),
        Op("SetGenres", (s, p, u, e, m, ct) => s.SetPlaylistGenresAsync(p, u, Array.Empty<long>(), ct)),
        Op("ResetGenres", (s, p, u, e, m, ct) => s.ResetPlaylistGenresAsync(p, u, ct)),
        Op("GenerateCover", (s, p, u, e, m, ct) => s.GeneratePlaylistCoverAsync(p, u, true, ct)),
        Op("PreviewCover", (s, p, u, e, m, ct) => s.PreviewPlaylistCoverAsync(p, u, ct)),
        Op("SetCover", (s, p, u, e, m, ct) => s.SetPlaylistCoverAsync(p, u, TinyPng, "image/png", ct)),
        Op("DeleteCover", (s, p, u, e, m, ct) => s.DeletePlaylistCoverAsync(p, u, ct)),
        Op("SetPublicTrueAsAdmin", (s, p, u, e, m, ct) => s.SetPlaylistPublicAsync(p, u, true, true, ct)),
        Op("SetPublicFalseAsAdmin", (s, p, u, e, m, ct) => s.SetPlaylistPublicAsync(p, u, true, false, ct)),
        Op("Delete", (s, p, u, e, m, ct) => s.DeletePlaylistAsync(p, u, ct)),
    };

    private static object[] Op(string name, Operation operation) => new object[] { name, operation };

    /// <summary>Asserts that the operation fails with exactly <see cref="PlaylistAccessDeniedException"/>; the failure message names the operation.</summary>
    /// <param name="name">The name of the operation under test.</param>
    /// <param name="action">The operation to run.</param>
    /// <returns>A task that completes when the assertion has been made.</returns>
    private static async Task AssertDeniedAsync(string name, Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        Assert.True(
            exception?.GetType() == typeof(PlaylistAccessDeniedException),
            $"{name}: expected {nameof(PlaylistAccessDeniedException)} but got {exception?.GetType().Name ?? "no exception"}.");
    }

    [Theory]
    [MemberData(nameof(ReadOperations))]
    public async Task Read_ForeignUser_PrivatePlaylist_IsDenied(string name, Operation operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, entryId, movieId) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: false);
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        await AssertDeniedAsync(name,
            () => operation(_service, playlistId, _testUserId, entryId, movieId, ct));
    }

    [Theory]
    [MemberData(nameof(ReadOperations))]
    public async Task Read_AdminWhoIsNotOwner_PrivatePlaylist_IsDenied(string name, Operation operation)
    {
        // Administrator status grants no read access to somebody else's private playlist: the service has
        // no notion of "admin" for reads at all.
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, entryId, movieId) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: false);

        await AssertDeniedAsync(name,
            () => operation(_service, playlistId, _testUserId, entryId, movieId, ct));
    }

    [Theory]
    [MemberData(nameof(ReadOperations))]
    public async Task Read_ForeignUser_PublicPlaylist_IsAllowed(string name, Operation operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, entryId, movieId) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: true, entryCount: 2);
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var exception = await Record.ExceptionAsync(() => operation(_service, playlistId, _testUserId, entryId, movieId, ct));

        Assert.True(exception is null, $"{name} threw {exception}.");
    }

    [Theory]
    [MemberData(nameof(ReadOperations))]
    public async Task Read_Owner_PrivatePlaylist_IsAllowed(string name, Operation operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, entryId, movieId) = await CreateOwnedPlaylistAsync(_testUserId, isPublic: false, entryCount: 2);
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var exception = await Record.ExceptionAsync(() => operation(_service, playlistId, _testUserId, entryId, movieId, ct));

        Assert.True(exception is null, $"{name} threw {exception}.");
    }

    [Theory]
    [MemberData(nameof(WriteOperations))]
    public async Task Write_ForeignUser_PrivatePlaylist_IsDenied(string name, Operation operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, entryId, movieId) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: false);
        var before = await SnapshotAsync(playlistId, ct);

        await AssertDeniedAsync(name,
            () => operation(_service, playlistId, _testUserId, entryId, movieId, ct));

        Assert.Equal(before, await SnapshotAsync(playlistId, ct));
    }

    [Theory]
    [MemberData(nameof(WriteOperations))]
    public async Task Write_ForeignUser_PublicPlaylist_IsDeniedAndChangesNothing(string name, Operation operation)
    {
        // The core of "ausschliesslich lesend": even though the viewer may read the public playlist, EVERY
        // write - including as an administrator (the SetPublic operations pass requesterIsAdmin = true) -
        // is refused with the same 403 exception, and the owner's data stays byte-for-byte unchanged.
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, entryId, movieId) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: true, entryCount: 2);
        var before = await SnapshotAsync(playlistId, ct);

        await AssertDeniedAsync(name,
            () => operation(_service, playlistId, _testUserId, entryId, movieId, ct));

        Assert.Equal(before, await SnapshotAsync(playlistId, ct));
    }

    [Fact]
    public async Task SetPublic_RegularUserAsOwner_IsDeniedAndStaysPrivate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, _, _) = await CreateOwnedPlaylistAsync(_testUserId, isPublic: false);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.SetPlaylistPublicAsync(playlistId, _testUserId, requesterIsAdmin: false, isPublic: true, ct));

        Assert.False((await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct)).IsPublic);
    }

    [Fact]
    public async Task SetPublic_RegularUserAsOwner_CannotClearFlagEither()
    {
        // "ausschliesslich Administratoren ... setzen und wieder entfernen".
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, _, _) = await CreateOwnedPlaylistAsync(_testUserId, isPublic: true);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.SetPlaylistPublicAsync(playlistId, _testUserId, requesterIsAdmin: false, isPublic: false, ct));

        Assert.True((await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct)).IsPublic);
    }

    [Fact]
    public async Task SetPublic_AdminAsOwner_SetsAndClearsFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, _, _) = await CreateOwnedPlaylistAsync(_testUserId, isPublic: false);

        var published = await _service.SetPlaylistPublicAsync(playlistId, _testUserId, requesterIsAdmin: true, isPublic: true, ct);
        Assert.True(published.IsPublic);
        Assert.True(published.IsOwner);
        Assert.True((await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct)).IsPublic);

        var unpublished = await _service.SetPlaylistPublicAsync(playlistId, _testUserId, requesterIsAdmin: true, isPublic: false, ct);
        Assert.False(unpublished.IsPublic);
        Assert.False((await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct)).IsPublic);
    }

    [Fact]
    public async Task SetPublic_AdminOnForeignPlaylist_IsDenied_ConservativeReading()
    {
        // Decision: an administrator may only mark playlists they OWN. Publishing another user's playlist
        // would require seeing it (contradicting "ihre Playlists bleiben stets privat").
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, _, _) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: false);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.SetPlaylistPublicAsync(playlistId, _testUserId, requesterIsAdmin: true, isPublic: true, ct));

        Assert.False((await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct)).IsPublic);
    }

    [Fact]
    public async Task SetPublic_UnknownPlaylist_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.SetPlaylistPublicAsync(999_999, _testUserId, requesterIsAdmin: true, isPublic: true, ct));
    }

    [Fact]
    public async Task Unpublish_RevokesViewerAccessImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, entryId, _) = await CreateOwnedPlaylistAsync(_testUserId, isPublic: true, entryCount: 2);
        await GrantMediaSourceAccessForUserAsync(_otherUserId);

        // Viewer (other user) can read while public ...
        Assert.NotNull(await _service.GetPlaylistAsync(playlistId, _otherUserId, ct));
        Assert.NotNull(await _service.StartPlaylistAsync(playlistId, _otherUserId, entryId, ct));

        await _service.SetPlaylistPublicAsync(playlistId, _testUserId, requesterIsAdmin: true, isPublic: false, ct);

        // ... and loses every kind of access the moment the flag is removed.
        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => _service.GetPlaylistAsync(playlistId, _otherUserId, ct));
        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => _service.GetPlaylistEntriesPagedAsync(playlistId, _otherUserId, 1, 10, ct));
        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => _service.StartPlaylistAsync(playlistId, _otherUserId, entryId, ct));
        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => _service.GetPlaylistCoverAsync(playlistId, _otherUserId, ct));
        Assert.DoesNotContain(await _service.GetPublicPlaylistsAsync(_otherUserId, null, ct), p => p.Id == playlistId);
    }

    [Fact]
    public async Task GetPublicPlaylists_ReturnsPublicOnes_OfOthersAndOwn_NeverPrivate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (foreignPublic, _, _) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: true);
        var (foreignPrivate, _, _) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: false);
        var (ownPublic, _, _) = await CreateOwnedPlaylistAsync(_testUserId, isPublic: true);
        var (ownPrivate, _, _) = await CreateOwnedPlaylistAsync(_testUserId, isPublic: false);

        var listed = (await _service.GetPublicPlaylistsAsync(_testUserId, null, ct)).Select(p => p.Id).ToList();

        Assert.Contains(foreignPublic, listed);
        Assert.Contains(ownPublic, listed);
        Assert.DoesNotContain(foreignPrivate, listed);
        Assert.DoesNotContain(ownPrivate, listed);
    }

    [Fact]
    public async Task GetPlaylists_OwnOverview_NeverIncludesForeignPublicPlaylists()
    {
        var ct = TestContext.Current.CancellationToken;
        var (foreignPublic, _, _) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: true);
        var (ownPublic, _, _) = await CreateOwnedPlaylistAsync(_testUserId, isPublic: true);

        var own = await _service.GetPlaylistsAsync(_testUserId, null, ct);

        Assert.DoesNotContain(own, p => p.Id == foreignPublic);
        var ownDto = Assert.Single(own, p => p.Id == ownPublic);
        Assert.True(ownDto.IsPublic);
    }

    [Fact]
    public async Task Dto_ForViewer_IsOwnerFalse_AndWithholdsOwnerOnlyEditingState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, _, _) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: true);
        var playlist = await _db.Playlists.SingleAsync(p => p.Id == playlistId, ct);
        playlist.GenresManuallyOverridden = true;
        playlist.CoverPictureIsUserUploaded = true;
        await _db.SaveChangesAsync(ct);
        var genreId = await GetOrCreateGenreIdAsync("Action");
        _db.PlaylistGenres.Add(new PlaylistGenre { PlaylistId = playlistId, GenreId = genreId, Count = 1 });
        await _db.SaveChangesAsync(ct);

        var viewerDto = await _service.GetPlaylistAsync(playlistId, _testUserId, ct);
        var ownerDto = await _service.GetPlaylistAsync(playlistId, _otherUserId, ct);

        Assert.NotNull(viewerDto);
        Assert.False(viewerDto!.IsOwner);
        Assert.True(viewerDto.IsPublic);
        Assert.False(viewerDto.GenresManuallyOverridden);
        Assert.False(viewerDto.CoverPictureIsUserUploaded);
        Assert.Empty(viewerDto.AllGenreIds);
        // What is needed to view the playlist stays visible.
        Assert.Equal("Action", Assert.Single(viewerDto.Genres).Name);

        Assert.NotNull(ownerDto);
        Assert.True(ownerDto!.IsOwner);
        Assert.True(ownerDto.GenresManuallyOverridden);
        Assert.True(ownerDto.CoverPictureIsUserUploaded);
        Assert.Equal(new[] { genreId }, ownerDto.AllGenreIds);

        // No owner identity anywhere in the DTO (no user id / e-mail leak).
        var json = System.Text.Json.JsonSerializer.Serialize(viewerDto);
        Assert.DoesNotContain(_otherUserId, json);
        Assert.DoesNotContain("other-user@test.com", json);
    }

    [Fact]
    public async Task Cover_ForeignUser_PrivatePlaylistCoverIsRefused_PublicCoverIsServed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (privateId, _, _) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: false);
        var (publicId, _, _) = await CreateOwnedPlaylistAsync(_otherUserId, isPublic: true);
        foreach (var id in new[] { privateId, publicId })
        {
            var picture = new Picture { Type = "cover", Data = TinyPng, ContentType = "image/png", PlaylistId = id };
            _db.Pictures.Add(picture);
            await _db.SaveChangesAsync(ct);
            (await _db.Playlists.SingleAsync(p => p.Id == id, ct)).CoverPictureId = picture.Id;
            await _db.SaveChangesAsync(ct);
        }

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => _service.GetPlaylistCoverAsync(privateId, _testUserId, ct));
        var served = await _service.GetPlaylistCoverAsync(publicId, _testUserId, ct);
        Assert.NotNull(served);
        Assert.Equal(TinyPng, served!.Data);
    }

    /// <summary>
    /// Creates a playlist for <paramref name="ownerId"/> with movie entries (in manual sort mode so
    /// reorder-type operations are meaningful), optionally public. Returns the playlist id and the id/media
    /// id of its FIRST entry.
    /// </summary>
    /// <param name="ownerId">The owning user.</param>
    /// <param name="isPublic">Whether the playlist is marked public.</param>
    /// <param name="entryCount">How many movie entries to create.</param>
    /// <returns>The playlist id, the first entry's id and its movie id.</returns>
    private async Task<(long PlaylistId, long EntryId, long MovieId)> CreateOwnedPlaylistAsync(string ownerId, bool isPublic, int entryCount = 1)
    {
        var movies = new List<(string, long, long?)>();
        for (var i = 0; i < entryCount; i++)
            movies.Add((MediaTypeValues.Movie, await CreateTestMediaEntryAsync(MediaTypeValues.Movie, $"Film {Guid.NewGuid()}"), i));

        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(ownerId, movies.ToArray());
        if (isPublic)
            await MakePlaylistPublicAsync(playlistId);

        var first = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).OrderBy(e => e.SortOrder).FirstAsync();
        return (playlistId, first.Id, first.MediaId);
    }

    /// <summary>A comparable snapshot of everything a non-owner must not be able to change.</summary>
    /// <param name="playlistId">The playlist to snapshot.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A string that changes whenever the playlist's data changes.</returns>
    private async Task<string> SnapshotAsync(long playlistId, CancellationToken ct)
    {
        var playlist = await _db.Playlists.AsNoTracking().SingleOrDefaultAsync(p => p.Id == playlistId, ct);
        if (playlist is null)
            return "<deleted>";

        var entries = await _db.PlaylistEntries.AsNoTracking()
            .Where(e => e.PlaylistId == playlistId)
            .OrderBy(e => e.Id)
            .Select(e => $"{e.Id}:{e.MediaType}:{e.MediaId}:{e.SortOrder}")
            .ToListAsync(ct);
        var exclusions = await _db.PlaylistEntryExclusions.AsNoTracking().CountAsync(x => x.PlaylistId == playlistId, ct);
        var genres = await _db.PlaylistGenres.AsNoTracking().CountAsync(g => g.PlaylistId == playlistId, ct);

        return $"{playlist.Name}|{playlist.Description}|{playlist.SortMode}|{playlist.IsPublic}|{playlist.UpdatedAt.Ticks}|" +
               $"{playlist.CoverPictureId}|{playlist.GenresManuallyOverridden}|{string.Join(",", entries)}|{exclusions}|{genres}";
    }
}
