using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.ReorderPlaylistEntryAsync"/> and
/// <see cref="VideoWebPlayer.Services.PlaylistService.BatchReorderPlaylistEntriesAsync"/>.
/// </summary>
public class PlaylistServiceTests_Reorder : PlaylistServiceTestBase
{
    [Fact]
    public async Task ReorderPlaylistEntry_Manual_SuccessfullyReorders()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, 1, 0),
            (MediaTypeValues.Movie, 2, 1));
        var entryId = await _db.PlaylistEntries.AsNoTracking().Where(e => e.MediaId == 1).Select(e => e.Id).SingleAsync(ct);

        await _service.ReorderPlaylistEntryAsync(playlistId, _testUserId, entryId, 5, ct);

        var updated = await _db.PlaylistEntries.AsNoTracking().SingleAsync(e => e.Id == entryId, ct);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task ReorderPlaylistEntry_NotOwner_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));
        var entryId = await _db.PlaylistEntries.AsNoTracking().Select(e => e.Id).SingleAsync(ct);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.ReorderPlaylistEntryAsync(playlistId, _otherUserId, entryId, 5, ct));
    }

    [Fact]
    public async Task ReorderPlaylistEntry_NotManualMode_ThrowsPlaylistNotInManualSortModeException()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        var entryId = await _db.PlaylistEntries.AsNoTracking().Select(e => e.Id).SingleAsync(ct);

        await Assert.ThrowsAsync<PlaylistNotInManualSortModeException>(
            () => _service.ReorderPlaylistEntryAsync(playlistId, _testUserId, entryId, 5, ct));
    }

    [Fact]
    public async Task ReorderPlaylistEntry_EntryNotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.ReorderPlaylistEntryAsync(playlistId, _testUserId, 999999, 5, ct));
    }

    [Fact]
    public async Task ReorderPlaylistEntry_NegativeSortOrder_ThrowsArgumentException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));
        var entryId = await _db.PlaylistEntries.AsNoTracking().Select(e => e.Id).SingleAsync(ct);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ReorderPlaylistEntryAsync(playlistId, _testUserId, entryId, -1, ct));
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_SuccessfullyReorders_Multiple()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie1Id = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film 1");
        var movie2Id = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film 2");
        var movie3Id = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film 3");
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movie1Id, 0),
            (MediaTypeValues.Movie, movie2Id, 1),
            (MediaTypeValues.Movie, movie3Id, 2));
        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        var entryForMedia1 = entries.Single(e => e.MediaId == movie1Id).Id;
        var entryForMedia2 = entries.Single(e => e.MediaId == movie2Id).Id;

        var result = await _service.BatchReorderPlaylistEntriesAsync(playlistId, _testUserId,
            new List<(long EntryId, long NewSortOrder)>
            {
                (entryForMedia1, 10),
                (entryForMedia2, 20)
            }, ct);

        Assert.Equal(2, result.Count());
        var updated1 = await _db.PlaylistEntries.AsNoTracking().SingleAsync(e => e.Id == entryForMedia1, ct);
        var updated2 = await _db.PlaylistEntries.AsNoTracking().SingleAsync(e => e.Id == entryForMedia2, ct);
        Assert.Equal(10, updated1.SortOrder);
        Assert.Equal(20, updated2.SortOrder);
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_DuplicateSortOrder_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, 1, 0),
            (MediaTypeValues.Movie, 2, 1));
        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        var entryIds = entries.Select(e => e.Id).ToList();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.BatchReorderPlaylistEntriesAsync(playlistId, _testUserId,
                new List<(long EntryId, long NewSortOrder)>
                {
                    (entryIds[0], 5),
                    (entryIds[1], 5)
                }, ct));
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_DuplicateEntryId_ThrowsArgumentException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));
        var entryId = await _db.PlaylistEntries.AsNoTracking().Select(e => e.Id).SingleAsync(ct);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.BatchReorderPlaylistEntriesAsync(playlistId, _testUserId,
                new List<(long EntryId, long NewSortOrder)>
                {
                    (entryId, 5),
                    (entryId, 6)
                }, ct));
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_EmptyList_ThrowsArgumentException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.BatchReorderPlaylistEntriesAsync(playlistId, _testUserId, new List<(long EntryId, long NewSortOrder)>(), ct));
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_EntryNotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.BatchReorderPlaylistEntriesAsync(playlistId, _testUserId,
                new List<(long EntryId, long NewSortOrder)> { (999999, 5) }, ct));
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_NotOwner_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));
        var entryId = await _db.PlaylistEntries.AsNoTracking().Select(e => e.Id).SingleAsync(ct);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.BatchReorderPlaylistEntriesAsync(playlistId, _otherUserId,
                new List<(long EntryId, long NewSortOrder)> { (entryId, 5) }, ct));
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_NotManualMode_ThrowsPlaylistNotInManualSortModeException()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        var entryId = await _db.PlaylistEntries.AsNoTracking().Select(e => e.Id).SingleAsync(ct);

        await Assert.ThrowsAsync<PlaylistNotInManualSortModeException>(
            () => _service.BatchReorderPlaylistEntriesAsync(playlistId, _testUserId,
                new List<(long EntryId, long NewSortOrder)> { (entryId, 5) }, ct));
    }

    [Fact]
    public async Task BatchReorderPlaylistEntries_NegativeSortOrder_ThrowsArgumentException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, 1, 0),
            (MediaTypeValues.Movie, 2, 1));
        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        var entryIds = entries.Select(e => e.Id).ToList();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.BatchReorderPlaylistEntriesAsync(playlistId, _testUserId,
                new List<(long EntryId, long NewSortOrder)>
                {
                    (entryIds[0], 5),
                    (entryIds[1], -1)
                }, ct));
    }

    /// <summary>
    /// Documents that BatchReorderPlaylistEntriesAsync only enforces uniqueness of NewSortOrder within
    /// the batch itself (see ReorderPlaylistEntryAsync's comment on the same design): it does not check
    /// for collisions with an existing entry that is not part of the request. Two entries ending up with
    /// the same SortOrder value (one moved by the batch, one left untouched) is allowed and resolved via
    /// the ThenBy(AddedAt) fallback when reading.
    /// </summary>
    [Fact]
    public async Task BatchReorderPlaylistEntries_CollidesWithEntryNotInRequest_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, 1, 0),
            (MediaTypeValues.Movie, 2, 1));
        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        var entryToMove = entries.Single(e => e.MediaId == 1).Id;
        var untouchedEntry = entries.Single(e => e.MediaId == 2);

        await _service.BatchReorderPlaylistEntriesAsync(playlistId, _testUserId,
            new List<(long EntryId, long NewSortOrder)> { (entryToMove, untouchedEntry.SortOrder!.Value) }, ct);

        var updatedMoved = await _db.PlaylistEntries.AsNoTracking().SingleAsync(e => e.Id == entryToMove, ct);
        Assert.Equal(untouchedEntry.SortOrder, updatedMoved.SortOrder);
    }
}
