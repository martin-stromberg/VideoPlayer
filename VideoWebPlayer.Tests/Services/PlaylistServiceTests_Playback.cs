using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for the playlist-playback-navigation methods of <see cref="PlaylistService"/>:
/// <see cref="PlaylistService.GetNextPlaylistEntryAsync"/>, <see cref="PlaylistService.GetPreviousPlaylistEntryAsync"/>,
/// <see cref="PlaylistService.StartPlaylistAsync"/> and <see cref="PlaylistService.AdvancePlaylistAsync"/>.
/// </summary>
public class PlaylistServiceTests_Playback : PlaylistServiceTestBase
{
    private async Task<long[]> GetOrderedEntryIdsAsync(long playlistId)
    {
        var entries = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, TestContext.Current.CancellationToken);
        return entries.Select(e => e.Id).ToArray();
    }

    private async Task<DtoPlaylistEntry[]> GetOrderedEntriesAsync(long playlistId)
        => await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task GetNextPlaylistEntryAsync_ByReleaseDateMode_ReturnsNextUnlockedEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (byReleaseDatePlaylistId, _) = await CreatePlaylistWithMultipleSortOrdersAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var entryIds = await GetOrderedEntryIdsAsync(byReleaseDatePlaylistId);

        var next = await _service.GetNextPlaylistEntryAsync(byReleaseDatePlaylistId, _testUserId, entryIds[0], ct);

        Assert.NotNull(next);
        Assert.Equal(entryIds[1], next!.Entry.Id);
        Assert.Equal(2, next.Position);
    }

    [Fact]
    public async Task GetNextPlaylistEntryAsync_ManualMode_ReturnsNextUnlockedEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, manualPlaylistId) = await CreatePlaylistWithMultipleSortOrdersAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var entryIds = await GetOrderedEntryIdsAsync(manualPlaylistId);

        var next = await _service.GetNextPlaylistEntryAsync(manualPlaylistId, _testUserId, entryIds[0], ct);

        Assert.NotNull(next);
        Assert.Equal(entryIds[1], next!.Entry.Id);
        Assert.Equal(2, next.Position);
    }

    [Fact]
    public async Task GetNextPlaylistEntryAsync_SkipsCollectionEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, _) = await CreateTestPlaylistWithMixedEntriesAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var orderedEntries = await GetOrderedEntriesAsync(playlistId);
        // Movie has no hierarchy, so it always sorts into the null-ParentId/earliest-AddedAt group and is
        // always first; TVShow/TVShowSeason/MovieCollection/TVShowEpisode may interleave depending on the
        // hierarchy ids the test fixture happens to assign, so the episode is located by media type rather
        // than by a hardcoded position.
        var movieEntryId = Assert.Single(orderedEntries, e => e.MediaType == MediaTypeValues.Movie).Id;
        var episodeEntry = Assert.Single(orderedEntries, e => e.MediaType == MediaTypeValues.TVShowEpisode);
        var expectedPosition = Array.IndexOf(orderedEntries, episodeEntry) + 1;

        var next = await _service.GetNextPlaylistEntryAsync(playlistId, _testUserId, movieEntryId, ct);

        Assert.NotNull(next);
        Assert.Equal(episodeEntry.Id, next!.Entry.Id);
        Assert.Equal(MediaTypeValues.TVShowEpisode, next.Entry.MediaType);
        // The skipped TVShow/TVShowSeason/MovieCollection entries between the movie and the episode must
        // still count towards the reported position - it must be the episode's actual position in the
        // full sort order, not the movie's position (1) incremented by one.
        Assert.Equal(expectedPosition, next.Position);
    }

    [Fact]
    public async Task GetNextPlaylistEntryAsync_SkipsLockedEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        // movieA and episodeC use the default MediaSourceId (1), which is granted source access below.
        // movieB uses a different, never-granted MediaSourceId, so it stays locked and must be skipped.
        var movieA = new Movie { Name = "Zugaenglicher Film", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movieA);
        var movieB = new Movie { Name = "Gesperrter Film", MediaSourceId = 99, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movieB);
        await _db.SaveChangesAsync(ct);
        var episodeCId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShowEpisode, "Zugaengliche Episode");

        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movieA.Id),
            (MediaTypeValues.Movie, movieB.Id),
            (MediaTypeValues.TVShowEpisode, episodeCId));
        await GrantMediaSourceAccessForUserAsync(_testUserId, mediaSourceId: 1);
        var entryIds = await GetOrderedEntryIdsAsync(playlistId);

        var next = await _service.GetNextPlaylistEntryAsync(playlistId, _testUserId, entryIds[0], ct);

        Assert.NotNull(next);
        Assert.Equal(entryIds[2], next!.Entry.Id);
        // The locked movieB at position 2 is skipped, so the reported position must be the episode's
        // actual position (3), not the caller's previous position (1) incremented by one.
        Assert.Equal(3, next.Position);
    }

    [Fact]
    public async Task GetNextPlaylistEntryAsync_ReturnsNullAtEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var (byReleaseDatePlaylistId, _) = await CreatePlaylistWithMultipleSortOrdersAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var entryIds = await GetOrderedEntryIdsAsync(byReleaseDatePlaylistId);

        var next = await _service.GetNextPlaylistEntryAsync(byReleaseDatePlaylistId, _testUserId, entryIds[^1], ct);

        Assert.Null(next);
    }

    [Fact]
    public async Task GetNextPlaylistEntryAsync_CurrentEntryNotInPlaylist_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetNextPlaylistEntryAsync(playlistId, _testUserId, 999999, ct));
    }

    [Fact]
    public async Task GetPreviousPlaylistEntryAsync_ReturnsPreviousUnlockedEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (byReleaseDatePlaylistId, _) = await CreatePlaylistWithMultipleSortOrdersAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var entryIds = await GetOrderedEntryIdsAsync(byReleaseDatePlaylistId);

        var previous = await _service.GetPreviousPlaylistEntryAsync(byReleaseDatePlaylistId, _testUserId, entryIds[2], ct);

        Assert.NotNull(previous);
        Assert.Equal(entryIds[1], previous!.Entry.Id);
        Assert.Equal(2, previous.Position);
    }

    [Fact]
    public async Task GetPreviousPlaylistEntryAsync_SkipsCollectionAndLockedEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, _) = await CreateTestPlaylistWithMixedEntriesAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var orderedEntries = await GetOrderedEntriesAsync(playlistId);
        // Movie has no hierarchy, so it always sorts first (see GetNextPlaylistEntryAsync_SkipsCollectionEntries).
        var movieEntryId = Assert.Single(orderedEntries, e => e.MediaType == MediaTypeValues.Movie).Id;
        var episodeEntryId = Assert.Single(orderedEntries, e => e.MediaType == MediaTypeValues.TVShowEpisode).Id;

        var previous = await _service.GetPreviousPlaylistEntryAsync(playlistId, _testUserId, episodeEntryId, ct);

        Assert.NotNull(previous);
        Assert.Equal(movieEntryId, previous!.Entry.Id);
        // The movie is always the first entry in this fixture (see GetNextPlaylistEntryAsync_SkipsCollectionEntries).
        Assert.Equal(1, previous.Position);
    }

    [Fact]
    public async Task GetPreviousPlaylistEntryAsync_ReturnsNullAtBeginning()
    {
        var ct = TestContext.Current.CancellationToken;
        var (byReleaseDatePlaylistId, _) = await CreatePlaylistWithMultipleSortOrdersAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var entryIds = await GetOrderedEntryIdsAsync(byReleaseDatePlaylistId);

        var previous = await _service.GetPreviousPlaylistEntryAsync(byReleaseDatePlaylistId, _testUserId, entryIds[0], ct);

        Assert.Null(previous);
    }

    [Fact]
    public async Task StartPlaylistAsync_ValidatesPlaylistOwnership()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.StartPlaylistAsync(playlistId, _otherUserId, entryId: null, ct));
    }

    [Fact]
    public async Task StartPlaylistAsync_PlaylistNotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.StartPlaylistAsync(999999, _testUserId, entryId: null, ct));
    }

    [Fact]
    public async Task StartPlaylistAsync_ValidatesEntryBelongsToPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.StartPlaylistAsync(playlistId, _testUserId, entryId: 999999, ct));
    }

    [Fact]
    public async Task StartPlaylistAsync_ExplicitEntryNotAccessible_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Gesperrte Serie");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));
        var entryIds = await GetOrderedEntryIdsAsync(playlistId);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.StartPlaylistAsync(playlistId, _testUserId, entryIds[0], ct));
    }

    [Fact]
    public async Task StartPlaylistAsync_StartsAtFirstIfNoEntryIdProvided()
    {
        var ct = TestContext.Current.CancellationToken;
        var (byReleaseDatePlaylistId, _) = await CreatePlaylistWithMultipleSortOrdersAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var entryIds = await GetOrderedEntryIdsAsync(byReleaseDatePlaylistId);

        var start = await _service.StartPlaylistAsync(byReleaseDatePlaylistId, _testUserId, entryId: null, ct);

        Assert.Equal(entryIds[0], start.CurrentEntryId);
        Assert.Equal(1, start.CurrentPosition);
    }

    [Fact]
    public async Task StartPlaylistAsync_NoPlayableAccessibleEntries_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Gesperrte Serie");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartPlaylistAsync(playlistId, _testUserId, entryId: null, ct));
    }

    [Fact]
    public async Task StartPlaylistAsync_ReturnsPlaybackStartDTO()
    {
        var ct = TestContext.Current.CancellationToken;
        var (byReleaseDatePlaylistId, _) = await CreatePlaylistWithMultipleSortOrdersAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var entryIds = await GetOrderedEntryIdsAsync(byReleaseDatePlaylistId);

        var start = await _service.StartPlaylistAsync(byReleaseDatePlaylistId, _testUserId, entryIds[1], ct);

        Assert.Equal(byReleaseDatePlaylistId, start.PlaylistId);
        Assert.False(string.IsNullOrWhiteSpace(start.PlaylistName));
        Assert.Equal(3, start.TotalCount);
        Assert.Equal(2, start.CurrentPosition);
        Assert.Equal(entryIds[1], start.CurrentEntryId);
        Assert.NotNull(start.CurrentEntry);
        // start.MediaType is the VideoPlayer-facing value ("movie"/"episode"), not the PascalCase
        // PlaylistEntry.MediaType ("Movie"/"TVShowEpisode") - see PlaylistEntryMediaTypeResolver.ToPlayerMediaType.
        Assert.Equal("movie", start.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(start.StreamUrl));
        Assert.Contains("/stream", start.StreamUrl);
    }

    [Fact]
    public async Task AdvancePlaylistAsync_CallsGetNextInternally()
    {
        var ct = TestContext.Current.CancellationToken;
        var (byReleaseDatePlaylistId, _) = await CreatePlaylistWithMultipleSortOrdersAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var entryIds = await GetOrderedEntryIdsAsync(byReleaseDatePlaylistId);

        var advanced = await _service.AdvancePlaylistAsync(byReleaseDatePlaylistId, _testUserId, entryIds[0], ct);
        var next = await _service.GetNextPlaylistEntryAsync(byReleaseDatePlaylistId, _testUserId, entryIds[0], ct);

        Assert.NotNull(advanced);
        Assert.NotNull(next);
        Assert.Equal(next!.Entry.Id, advanced!.Entry.Id);
        Assert.Equal(next.Position, advanced.Position);
    }

    [Fact]
    public async Task AdvancePlaylistAsync_ReturnsNextEntryOrNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var (byReleaseDatePlaylistId, _) = await CreatePlaylistWithMultipleSortOrdersAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var entryIds = await GetOrderedEntryIdsAsync(byReleaseDatePlaylistId);

        var midAdvance = await _service.AdvancePlaylistAsync(byReleaseDatePlaylistId, _testUserId, entryIds[0], ct);
        var endAdvance = await _service.AdvancePlaylistAsync(byReleaseDatePlaylistId, _testUserId, entryIds[^1], ct);

        Assert.NotNull(midAdvance);
        Assert.Null(endAdvance);
    }
}
