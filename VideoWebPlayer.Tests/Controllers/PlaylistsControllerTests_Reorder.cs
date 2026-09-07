using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for the reorder endpoints of <see cref="VideoWebPlayer.Controllers.PlaylistsController"/>:
/// <c>ReorderPlaylistEntry</c> and <c>BatchReorderPlaylistEntries</c>.
/// </summary>
public class PlaylistsControllerTests_Reorder : PlaylistsControllerTestBase
{
    private async Task<long> CreateManualPlaylistAsync(string name = "Manuelle Playlist")
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = name, SortMode = PlaylistSortModeValues.Manual });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;
        return created!.Id;
    }

    private async Task<long> AddEntryAsync(long playlistId, string name)
    {
        var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();

        var result = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movie.Id });
        var dto = Assert.IsType<DtoPlaylistAddResult>(Assert.IsType<OkObjectResult>(result).Value);
        return dto.TopLevelEntry!.Id;
    }

    [Fact]
    public async Task ReorderPlaylistEntry_ValidRequest_Returns200Ok()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId = await AddEntryAsync(playlistId, "Film 1");

        var result = await _controller.ReorderPlaylistEntry(playlistId, entryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = 5 });

        Assert.IsType<OkResult>(result);
        var entry = await _db.PlaylistEntries.AsNoTracking().SingleAsync(e => e.Id == entryId, TestContext.Current.CancellationToken);
        Assert.Equal(5, entry.SortOrder);
    }

    [Fact]
    public async Task ReorderPlaylistEntry_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId = await AddEntryAsync(playlistId, "Film 1");
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.ReorderPlaylistEntry(playlistId, entryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = 5 });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ReorderPlaylistEntry_NotManualMode_Returns409Conflict()
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Automatische Playlist" });
        var playlistId = (Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist)!.Id;
        var entryId = await AddEntryAsync(playlistId, "Film 1");

        var result = await _controller.ReorderPlaylistEntry(playlistId, entryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = 5 });

        // Unified with BatchReorderPlaylistEntries' NotManualMode response below: both endpoints now
        // return 409 for this business rule (the single-entry endpoint used to return 400).
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task ReorderPlaylistEntry_EntryNotFound_Returns404NotFound()
    {
        var playlistId = await CreateManualPlaylistAsync();

        var result = await _controller.ReorderPlaylistEntry(playlistId, 999999, new DtoReorderPlaylistEntryRequest { NewSortOrder = 5 });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ReorderPlaylistEntry_NegativeSortOrder_Returns400BadRequest()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId = await AddEntryAsync(playlistId, "Film 1");

        var result = await _controller.ReorderPlaylistEntry(playlistId, entryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = -1 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_ValidRequest_Returns200Ok()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId1 = await AddEntryAsync(playlistId, "Film 1");
        var entryId2 = await AddEntryAsync(playlistId, "Film 2");

        var result = await _controller.BatchReorderPlaylistEntries(playlistId, new DtoBatchReorderPlaylistEntriesRequest
        {
            ReorderOperations = new List<DtoReorderOperation>
            {
                new() { EntryId = entryId1, NewSortOrder = 10 },
                new() { EntryId = entryId2, NewSortOrder = 20 }
            }
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var entries = Assert.IsAssignableFrom<IEnumerable<DtoPlaylistEntry>>(okResult.Value).ToList();
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_DuplicateSortOrder_Returns409Conflict()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId1 = await AddEntryAsync(playlistId, "Film 1");
        var entryId2 = await AddEntryAsync(playlistId, "Film 2");

        var result = await _controller.BatchReorderPlaylistEntries(playlistId, new DtoBatchReorderPlaylistEntriesRequest
        {
            ReorderOperations = new List<DtoReorderOperation>
            {
                new() { EntryId = entryId1, NewSortOrder = 10 },
                new() { EntryId = entryId2, NewSortOrder = 10 }
            }
        });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId = await AddEntryAsync(playlistId, "Film 1");
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.BatchReorderPlaylistEntries(playlistId, new DtoBatchReorderPlaylistEntriesRequest
        {
            ReorderOperations = new List<DtoReorderOperation> { new() { EntryId = entryId, NewSortOrder = 5 } }
        });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_NotManualMode_Returns409Conflict()
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Automatische Playlist" });
        var playlistId = (Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist)!.Id;
        var entryId = await AddEntryAsync(playlistId, "Film 1");

        var result = await _controller.BatchReorderPlaylistEntries(playlistId, new DtoBatchReorderPlaylistEntriesRequest
        {
            ReorderOperations = new List<DtoReorderOperation> { new() { EntryId = entryId, NewSortOrder = 5 } }
        });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_EntryNotFound_Returns404NotFound()
    {
        var playlistId = await CreateManualPlaylistAsync();

        var result = await _controller.BatchReorderPlaylistEntries(playlistId, new DtoBatchReorderPlaylistEntriesRequest
        {
            ReorderOperations = new List<DtoReorderOperation> { new() { EntryId = 999999, NewSortOrder = 5 } }
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
