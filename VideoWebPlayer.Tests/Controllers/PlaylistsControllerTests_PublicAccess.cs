using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Access matrix of Entwicklungsschritt 11 (oeffentliche Playlists) at HTTP level: roles (owner, foreign
/// regular user, administrator who is not the owner, administrator as owner, not logged in) x every
/// endpoint of <see cref="PlaylistsController"/> (reading and writing) x playlist private/public. The
/// server - not the UI - must refuse every write by a non-owner with 403. <c>_user</c> is the regular
/// (viewer) user, <c>_otherUser</c> owns the playlists under test unless stated otherwise.
/// </summary>
public class PlaylistsControllerTests_PublicAccess : PlaylistsControllerTestBase
{
    /// <summary>An endpoint call: the controller, the playlist id, one entry id and its media id.</summary>
    /// <param name="controller">The controller under test.</param>
    /// <param name="id">The playlist id.</param>
    /// <param name="entryId">The id of an existing entry.</param>
    /// <param name="mediaId">The media id of that entry.</param>
    /// <returns>The endpoint's result.</returns>
    public delegate Task<IActionResult> Endpoint(PlaylistsController controller, long id, long entryId, long mediaId);

    /// <summary>The reading endpoints (owner, or anybody while public).</summary>
    /// <returns>Theory data: endpoint name and call.</returns>
    public static IEnumerable<object[]> ReadEndpoints() => new[]
    {
        Ep("GetPlaylist", (c, id, e, m) => c.GetPlaylist(id)),
        Ep("GetPlaylistEntries", (c, id, e, m) => c.GetPlaylistEntries(id)),
        Ep("GetPlaylistEntriesPaged", (c, id, e, m) => c.GetPlaylistEntriesPaged(id)),
        Ep("StartPlaylist", (c, id, e, m) => c.StartPlaylist(id)),
        Ep("StartPlaylistAtEntry", (c, id, e, m) => c.StartPlaylist(id, e)),
        Ep("Next", (c, id, e, m) => c.GetNextPlaylistEntry(id, e)),
        Ep("Previous", (c, id, e, m) => c.GetPreviousPlaylistEntry(id, e)),
        Ep("Advance", (c, id, e, m) => c.AdvancePlaylist(id, e)),
        Ep("GetCover", (c, id, e, m) => c.GetPlaylistCover(id)),
    };

    /// <summary>The writing endpoints (owner only, never anybody else - public or not).</summary>
    /// <returns>Theory data: endpoint name and call.</returns>
    public static IEnumerable<object[]> WriteEndpoints() => new[]
    {
        Ep("UpdatePlaylist", (c, id, e, m) => c.UpdatePlaylist(id, new DtoUpdatePlaylistRequest { Name = "Umbenannt" })),
        Ep("AddMediaToPlaylist", (c, id, e, m) => c.AddMediaToPlaylist(id, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = m })),
        Ep("RemoveMediaFromPlaylist", (c, id, e, m) => c.RemoveMediaFromPlaylist(id, MediaTypeValues.Movie, m)),
        Ep("RemoveMediaFromPlaylistConfirmed", (c, id, e, m) => c.RemoveMediaFromPlaylist(id, MediaTypeValues.Movie, m, true)),
        Ep("ReorderPlaylistEntry", (c, id, e, m) => c.ReorderPlaylistEntry(id, e, new DtoReorderPlaylistEntryRequest { NewSortOrder = 4 })),
        Ep("BatchReorder", (c, id, e, m) => c.BatchReorderPlaylistEntries(id, new DtoBatchReorderPlaylistEntriesRequest
        {
            ReorderOperations = new List<DtoReorderOperation> { new() { EntryId = e, NewSortOrder = 4 } }
        })),
        Ep("GetMaxSortOrder", (c, id, e, m) => c.GetMaxSortOrder(id)),
        Ep("MoveEntryToBeginning", (c, id, e, m) => c.MoveEntryToBeginning(id, e)),
        Ep("MoveEntryBetween", (c, id, e, m) => c.MoveEntryBetween(id, e, new DtoReorderPlaylistEntryRequest { NewSortOrder = 3 })),
        Ep("ChangeSortMode", (c, id, e, m) => c.ChangeSortMode(id, new DtoChangeSortModeRequest { NewSortMode = PlaylistSortModeValues.Manual })),
        Ep("SetPlaylistGenres", (c, id, e, m) => c.SetPlaylistGenres(id, new DtoSetPlaylistGenresRequest { GenreIds = Array.Empty<long>() })),
        Ep("ResetPlaylistGenres", (c, id, e, m) => c.ResetPlaylistGenres(id)),
        Ep("UploadPlaylistCover", (c, id, e, m) => c.UploadPlaylistCover(id, CreateFormFile())),
        Ep("RegeneratePlaylistCover", (c, id, e, m) => c.RegeneratePlaylistCover(id, true)),
        Ep("DeletePlaylistCover", (c, id, e, m) => c.DeletePlaylistCover(id)),
        Ep("SetPlaylistPublicTrue", (c, id, e, m) => c.SetPlaylistPublic(id, new DtoSetPlaylistPublicRequest { IsPublic = true })),
        Ep("SetPlaylistPublicFalse", (c, id, e, m) => c.SetPlaylistPublic(id, new DtoSetPlaylistPublicRequest { IsPublic = false })),
        Ep("DeletePlaylist", (c, id, e, m) => c.DeletePlaylist(id)),
    };

    /// <summary>Every endpoint - reads, writes and the public overview.</summary>
    /// <returns>Theory data: endpoint name and call.</returns>
    public static IEnumerable<object[]> AllEndpoints()
        => ReadEndpoints().Concat(WriteEndpoints()).Append(Ep("GetPlaylists", (c, id, e, m) => c.GetPlaylists())).Append(Ep("GetPublicPlaylists", (c, id, e, m) => c.GetPublicPlaylists()));

    private static object[] Ep(string name, Endpoint endpoint) => new object[] { name, endpoint };

    [Theory]
    [MemberData(nameof(ReadEndpoints))]
    [MemberData(nameof(WriteEndpoints))]
    public async Task ForeignUser_PrivatePlaylist_EveryEndpointIsForbidden(string name, Endpoint endpoint)
    {
        var (playlistId, entryId, mediaId) = await CreatePlaylistForAsync(_otherUser, isPublic: false);
        await GrantMediaSourceAccessAsync();
        _fakeAuth.CurrentUser = _user;

        var result = await endpoint(_controller, playlistId, entryId, mediaId);

        Assert.IsType<ForbidResult>(result);
    }

    [Theory]
    [MemberData(nameof(ReadEndpoints))]
    public async Task ForeignUser_PublicPlaylist_ReadEndpointsAreAllowed(string name, Endpoint endpoint)
    {
        var (playlistId, entryId, mediaId) = await CreatePlaylistForAsync(_otherUser, isPublic: true, entryCount: 2);
        await GrantMediaSourceAccessAsync();
        _fakeAuth.CurrentUser = _user;

        var result = await endpoint(_controller, playlistId, entryId, mediaId);

        // (A playlist without a cover answers the cover endpoint with 404 - still "allowed", just nothing to serve.)
        Assert.True(result is OkObjectResult or NoContentResult or FileContentResult || (name == "GetCover" && result is NotFoundResult),
            $"{name} returned {result.GetType().Name} instead of a success result.");
    }

    [Theory]
    [MemberData(nameof(WriteEndpoints))]
    public async Task ForeignUser_PublicPlaylist_EveryWriteEndpointIsForbiddenAndChangesNothing(string name, Endpoint endpoint)
    {
        var (playlistId, entryId, mediaId) = await CreatePlaylistForAsync(_otherUser, isPublic: true, entryCount: 2);
        await GrantMediaSourceAccessAsync();
        _fakeAuth.CurrentUser = _user;
        var before = await SnapshotAsync(playlistId);

        var result = await endpoint(_controller, playlistId, entryId, mediaId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await SnapshotAsync(playlistId));
    }

    [Theory]
    [MemberData(nameof(WriteEndpoints))]
    public async Task AdminWhoIsNotOwner_PublicPlaylist_EveryWriteEndpointIsForbiddenAndChangesNothing(string name, Endpoint endpoint)
    {
        // Administrators get no editing rights on somebody else's playlist - not even the flag toggle.
        var (playlistId, entryId, mediaId) = await CreatePlaylistForAsync(_otherUser, isPublic: true, entryCount: 2);
        _fakeAuth.CurrentUser = await CreateAdminAsync();
        var before = await SnapshotAsync(playlistId);

        var result = await endpoint(_controller, playlistId, entryId, mediaId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(before, await SnapshotAsync(playlistId));
    }

    [Theory]
    [MemberData(nameof(ReadEndpoints))]
    public async Task AdminWhoIsNotOwner_PrivatePlaylist_ReadEndpointsAreForbidden(string name, Endpoint endpoint)
    {
        var (playlistId, entryId, mediaId) = await CreatePlaylistForAsync(_otherUser, isPublic: false);
        _fakeAuth.CurrentUser = await CreateAdminAsync();

        var result = await endpoint(_controller, playlistId, entryId, mediaId);

        Assert.IsType<ForbidResult>(result);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task NotLoggedIn_EveryEndpointIsUnauthorized(string name, Endpoint endpoint)
    {
        var (playlistId, entryId, mediaId) = await CreatePlaylistForAsync(_otherUser, isPublic: true);
        _fakeAuth.CurrentUser = null;

        var result = await endpoint(_controller, playlistId, entryId, mediaId);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SetPlaylistPublic_RegularUserAsOwner_Returns403AndStaysPrivate()
    {
        var (playlistId, _, _) = await CreatePlaylistForAsync(_user, isPublic: false);
        _fakeAuth.CurrentUser = _user;

        var result = await _controller.SetPlaylistPublic(playlistId, new DtoSetPlaylistPublicRequest { IsPublic = true });

        Assert.IsType<ForbidResult>(result);
        Assert.False((await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId)).IsPublic);
    }

    [Fact]
    public async Task SetPlaylistPublic_AdminAsOwner_SetsAndClearsFlag_ViewerLosesAccessImmediately()
    {
        var admin = await CreateAdminAsync();
        var (playlistId, _, _) = await CreatePlaylistForAsync(admin, isPublic: false);
        await GrantMediaSourceAccessAsync();

        _fakeAuth.CurrentUser = admin;
        var published = Assert.IsType<OkObjectResult>(await _controller.SetPlaylistPublic(playlistId, new DtoSetPlaylistPublicRequest { IsPublic = true }));
        Assert.True(Assert.IsType<DtoPlaylist>(published.Value).IsPublic);

        _fakeAuth.CurrentUser = _user;
        var viewerView = Assert.IsType<OkObjectResult>(await _controller.GetPlaylist(playlistId));
        var viewerDto = Assert.IsType<DtoPlaylist>(viewerView.Value);
        Assert.True(viewerDto.IsPublic);
        Assert.False(viewerDto.IsOwner);

        _fakeAuth.CurrentUser = admin;
        var cleared = Assert.IsType<OkObjectResult>(await _controller.SetPlaylistPublic(playlistId, new DtoSetPlaylistPublicRequest { IsPublic = false }));
        Assert.False(Assert.IsType<DtoPlaylist>(cleared.Value).IsPublic);

        _fakeAuth.CurrentUser = _user;
        Assert.IsType<ForbidResult>(await _controller.GetPlaylist(playlistId));
        Assert.IsType<ForbidResult>(await _controller.GetPlaylistEntriesPaged(playlistId));
        Assert.IsType<ForbidResult>(await _controller.StartPlaylist(playlistId));
    }

    [Fact]
    public async Task SetPlaylistPublic_UnknownPlaylist_Returns404()
    {
        _fakeAuth.CurrentUser = await CreateAdminAsync();

        var result = await _controller.SetPlaylistPublic(999_999, new DtoSetPlaylistPublicRequest { IsPublic = true });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetPublicPlaylists_ListsOnlyPublicPlaylists_AndOwnOverviewNeverListsForeignOnes()
    {
        var (foreignPublic, _, _) = await CreatePlaylistForAsync(_otherUser, isPublic: true);
        var (foreignPrivate, _, _) = await CreatePlaylistForAsync(_otherUser, isPublic: false);
        _fakeAuth.CurrentUser = _user;

        var publicList = Assert.IsType<DtoPlaylist[]>(Assert.IsType<OkObjectResult>(await _controller.GetPublicPlaylists()).Value);
        var ownList = Assert.IsType<DtoPlaylist[]>(Assert.IsType<OkObjectResult>(await _controller.GetPlaylists()).Value);

        Assert.Contains(publicList, p => p.Id == foreignPublic);
        Assert.DoesNotContain(publicList, p => p.Id == foreignPrivate);
        Assert.All(publicList, p => Assert.False(p.IsOwner));
        Assert.Empty(ownList);
    }

    [Fact]
    public async Task GetCover_PublicPlaylist_DeliversImageToViewer_PrivateOneDoesNot()
    {
        var (publicId, _, _) = await CreatePlaylistForAsync(_otherUser, isPublic: true, withCover: true);
        var (privateId, _, _) = await CreatePlaylistForAsync(_otherUser, isPublic: false, withCover: true);
        _fakeAuth.CurrentUser = _user;

        var served = Assert.IsType<FileContentResult>(await _controller.GetPlaylistCover(publicId));
        Assert.Equal("image/png", served.ContentType);
        Assert.IsType<ForbidResult>(await _controller.GetPlaylistCover(privateId));
    }

    /// <summary>
    /// Creates a playlist (manual sort mode, movie entries) directly in the database for the given owner.
    /// </summary>
    /// <param name="owner">The owning user.</param>
    /// <param name="isPublic">Whether to mark the playlist public.</param>
    /// <param name="entryCount">How many movie entries to create.</param>
    /// <param name="withCover">Whether to give the playlist a cover picture.</param>
    /// <returns>The playlist id, the id of its first entry and that entry's media id.</returns>
    private async Task<(long PlaylistId, long EntryId, long MediaId)> CreatePlaylistForAsync(ApplicationUser owner, bool isPublic, int entryCount = 1, bool withCover = false)
    {
        var playlist = new Playlist
        {
            UserId = owner.Id,
            Name = $"Playlist {Guid.NewGuid()}",
            SortMode = PlaylistSortMode.Manual,
            IsPublic = isPublic
        };
        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync();

        for (var i = 0; i < entryCount; i++)
        {
            var movieId = await CreateMovieAsync($"Film {Guid.NewGuid()}");
            _db.PlaylistEntries.Add(new PlaylistEntry { PlaylistId = playlist.Id, MediaType = MediaTypeValues.Movie, MediaId = movieId, SortOrder = i, AddedAt = DateTime.UtcNow });
        }

        await _db.SaveChangesAsync();

        if (withCover)
        {
            var picture = new Picture { Type = "cover", Data = new byte[] { 1, 2, 3 }, ContentType = "image/png", PlaylistId = playlist.Id };
            _db.Pictures.Add(picture);
            await _db.SaveChangesAsync();
            playlist.CoverPictureId = picture.Id;
            await _db.SaveChangesAsync();
        }

        var first = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlist.Id).OrderBy(e => e.SortOrder).FirstAsync();
        return (playlist.Id, first.Id, first.MediaId);
    }

    /// <summary>Creates and persists an administrator user.</summary>
    /// <returns>The administrator.</returns>
    private async Task<ApplicationUser> CreateAdminAsync()
    {
        var admin = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "admin@test.com", IsAdmin = true };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();
        return admin;
    }

    /// <summary>Everything a non-owner must not be able to change, as a comparable string.</summary>
    /// <param name="playlistId">The playlist to snapshot.</param>
    /// <returns>A string that changes whenever the playlist's data changes.</returns>
    private async Task<string> SnapshotAsync(long playlistId)
    {
        var playlist = await _db.Playlists.AsNoTracking().SingleOrDefaultAsync(p => p.Id == playlistId);
        if (playlist is null)
            return "<deleted>";

        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).OrderBy(e => e.Id)
            .Select(e => $"{e.Id}:{e.MediaId}:{e.SortOrder}").ToListAsync();
        return $"{playlist.Name}|{playlist.Description}|{playlist.SortMode}|{playlist.IsPublic}|{playlist.CoverPictureId}|{string.Join(",", entries)}";
    }

    private static IFormFile CreateFormFile()
    {
        var bytes = new byte[] { 137, 80, 78, 71 };
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "cover.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }
}
