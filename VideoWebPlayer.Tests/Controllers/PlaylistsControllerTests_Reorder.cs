using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for the reorder endpoints of <see cref="VideoWebPlayer.Controllers.PlaylistsController"/>:
/// <c>ReorderPlaylistEntry</c>, <c>BatchReorderPlaylistEntries</c> and <c>MoveEntryBetween</c>.
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

    [Fact]
    public async Task MoveEntryBetween_ValidRequest_Returns200Ok()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId1 = await AddEntryAsync(playlistId, "Film 1");
        var entryId2 = await AddEntryAsync(playlistId, "Film 2");

        var result = await _controller.MoveEntryBetween(playlistId, entryId2, new DtoReorderPlaylistEntryRequest { NewSortOrder = 0 });

        Assert.IsType<OkResult>(result);
        var entry1 = await _db.PlaylistEntries.AsNoTracking().SingleAsync(e => e.Id == entryId1, TestContext.Current.CancellationToken);
        var entry2 = await _db.PlaylistEntries.AsNoTracking().SingleAsync(e => e.Id == entryId2, TestContext.Current.CancellationToken);
        Assert.Equal(1, entry1.SortOrder);
        Assert.Equal(0, entry2.SortOrder);
    }

    [Fact]
    public async Task MoveEntryBetween_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId = await AddEntryAsync(playlistId, "Film 1");
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.MoveEntryBetween(playlistId, entryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = 0 });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task MoveEntryBetween_EntryNotFound_Returns404NotFound()
    {
        var playlistId = await CreateManualPlaylistAsync();

        var result = await _controller.MoveEntryBetween(playlistId, 999999, new DtoReorderPlaylistEntryRequest { NewSortOrder = 0 });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task MoveEntryBetween_NotManualMode_Returns409Conflict()
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Automatische Playlist" });
        var playlistId = (Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist)!.Id;
        var entryId = await AddEntryAsync(playlistId, "Film 1");

        var result = await _controller.MoveEntryBetween(playlistId, entryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = 0 });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task MoveEntryBetween_NegativeSortOrder_Returns400BadRequest()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId = await AddEntryAsync(playlistId, "Film 1");

        var result = await _controller.MoveEntryBetween(playlistId, entryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = -1 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Pins the status code documented in <c>docs/API.md</c> for an entry without a manual sort position:
    /// <c>PlaylistEntryReorderService.MoveEntryBetweenAsync</c> throws a plain
    /// <see cref="InvalidOperationException"/> for this case, and <c>MoveEntryBetween</c> passes no
    /// <c>mapInvalidOperation</c>, so the generic branch answers with 400 Bad Request — not with the
    /// 409 Conflict that only the "not in manual sort mode" case produces.
    /// </summary>
    [Fact]
    public async Task MoveEntryBetween_EntryWithoutSortOrder_Returns400BadRequest()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId = await AddEntryAsync(playlistId, "Film 1");
        await ClearSortOrderAsync(entryId);

        var result = await _controller.MoveEntryBetween(playlistId, entryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = 0 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Counterpart of <see cref="MoveEntryBetween_EntryWithoutSortOrder_Returns400BadRequest"/> for
    /// <c>MoveEntryToBeginning</c>, which delegates to the same service method with target position 0 and
    /// therefore answers with the same 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task MoveEntryToBeginning_EntryWithoutSortOrder_Returns400BadRequest()
    {
        var playlistId = await CreateManualPlaylistAsync();
        var entryId = await AddEntryAsync(playlistId, "Film 1");
        await ClearSortOrderAsync(entryId);

        var result = await _controller.MoveEntryToBeginning(playlistId, entryId);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Removes the manual sort position of a playlist entry directly in the database, the state the
    /// reorder service rejects.
    /// </summary>
    /// <param name="entryId">The playlist entry identifier.</param>
    private async Task ClearSortOrderAsync(long entryId)
    {
        var entry = await _db.PlaylistEntries.SingleAsync(e => e.Id == entryId, TestContext.Current.CancellationToken);
        entry.SortOrder = null;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
