using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.ChangeSortModeAsync"/>.
/// </summary>
public class PlaylistServiceTests_SortMode : PlaylistServiceTestBase
{
    [Fact]
    public async Task ChangeSortModeAsync_ManualToByReleaseDate_WithoutConfirmation_ThrowsManualSortOrderConfirmationRequiredException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));

        var ex = await Assert.ThrowsAsync<ManualSortOrderConfirmationRequiredException>(
            () => _service.ChangeSortModeAsync(playlistId, _testUserId, PlaylistSortModeValues.ByReleaseDate, false, ct));

        Assert.Equal("Die manuelle Reihenfolge geht beim Wechsel verloren.", ex.Message);
        var playlist = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct);
        Assert.Equal(PlaylistSortMode.Manual, playlist.SortMode);
    }

    [Fact]
    public async Task ChangeSortModeAsync_ManualToByReleaseDate_WithoutConfirmationParameter_ThrowsManualSortOrderConfirmationRequiredException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));

        await Assert.ThrowsAsync<ManualSortOrderConfirmationRequiredException>(
            () => _service.ChangeSortModeAsync(playlistId, _testUserId, PlaylistSortModeValues.ByReleaseDate, null, ct));
    }

    [Fact]
    public async Task ChangeSortModeAsync_ManualToByReleaseDate_WithConfirmation_ClearsSortOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, 1, 0),
            (MediaTypeValues.Movie, 2, 1));

        var result = await _service.ChangeSortModeAsync(playlistId, _testUserId, PlaylistSortModeValues.ByReleaseDate, true, ct);

        Assert.Equal(PlaylistSortModeValues.ByReleaseDate, result.SortMode);
        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        Assert.All(entries, e => Assert.Null(e.SortOrder));
    }

    [Fact]
    public async Task ChangeSortModeAsync_ByReleaseDateToManual_PopulatesSortOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithReleaseDatesAsync(_testUserId,
            (MediaTypeValues.Movie, "Zuletzt", new DateTime(2022, 1, 1)),
            (MediaTypeValues.Movie, "Zuerst", new DateTime(2020, 1, 1)));

        var result = await _service.ChangeSortModeAsync(playlistId, _testUserId, PlaylistSortModeValues.Manual, null, ct);

        Assert.Equal(PlaylistSortModeValues.Manual, result.SortMode);
        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        Assert.All(entries, e => Assert.NotNull(e.SortOrder));
    }

    [Fact]
    public async Task ChangeSortModeAsync_ByReleaseDateToManual_AssignsAscendingSortOrderMatchingReleaseDateOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithReleaseDatesAsync(_testUserId,
            (MediaTypeValues.Movie, "Zuletzt", new DateTime(2022, 1, 1)),
            (MediaTypeValues.Movie, "Zuerst", new DateTime(2020, 1, 1)));

        await _service.ChangeSortModeAsync(playlistId, _testUserId, PlaylistSortModeValues.Manual, null, ct);

        var pagedResult = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);
        Assert.Equal(new[] { "Zuerst", "Zuletzt" }, pagedResult.Entries.Select(e => e.MediaTitle));
        Assert.Equal(new long?[] { 0, 1 }, pagedResult.Entries.Select(e => e.SortOrder));
    }

    [Fact]
    public async Task ChangeSortModeAsync_NotOwner_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.ChangeSortModeAsync(playlistId, _otherUserId, PlaylistSortModeValues.ByReleaseDate, true, ct));
    }

    [Fact]
    public async Task ChangeSortModeAsync_InvalidSortMode_ThrowsArgumentException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 0));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ChangeSortModeAsync(playlistId, _testUserId, "UnknownMode", true, ct));
    }

    [Fact]
    public async Task ChangeSortModeAsync_SameModeAgain_NoOpSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 1, 5));

        var result = await _service.ChangeSortModeAsync(playlistId, _testUserId, PlaylistSortModeValues.Manual, null, ct);

        Assert.Equal(PlaylistSortModeValues.Manual, result.SortMode);
        var entry = await _db.PlaylistEntries.AsNoTracking().SingleAsync(e => e.PlaylistId == playlistId, ct);
        Assert.Equal(5, entry.SortOrder);
    }
}
