using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.GetPlaylistEntriesPagedAsync"/>.
/// </summary>
public class PlaylistServiceTests_GetEntriesPaged : PlaylistServiceTestBase
{
    [Fact]
    public async Task GetEntriesPaged_ReturnsFirstPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithReleaseDatesAsync(_testUserId,
            (MediaTypeValues.Movie, "Film 1", new DateTime(2020, 1, 1)),
            (MediaTypeValues.Movie, "Film 2", new DateTime(2020, 2, 1)),
            (MediaTypeValues.Movie, "Film 3", new DateTime(2020, 3, 1)),
            (MediaTypeValues.Movie, "Film 4", new DateTime(2020, 4, 1)),
            (MediaTypeValues.Movie, "Film 5", new DateTime(2020, 5, 1)));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 3, ct);

        Assert.Equal(3, result.Entries.Length);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(3, result.PageSize);
    }

    [Fact]
    public async Task GetEntriesPaged_HasNextPageTrue_WhenMoreEntriesExist()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithReleaseDatesAsync(_testUserId,
            (MediaTypeValues.Movie, "Film 1", new DateTime(2020, 1, 1)),
            (MediaTypeValues.Movie, "Film 2", new DateTime(2020, 2, 1)),
            (MediaTypeValues.Movie, "Film 3", new DateTime(2020, 3, 1)),
            (MediaTypeValues.Movie, "Film 4", new DateTime(2020, 4, 1)),
            (MediaTypeValues.Movie, "Film 5", new DateTime(2020, 5, 1)));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 3, ct);

        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task GetEntriesPaged_HasNextPageFalse_WhenNoMoreEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithReleaseDatesAsync(_testUserId,
            (MediaTypeValues.Movie, "Film 1", new DateTime(2020, 1, 1)),
            (MediaTypeValues.Movie, "Film 2", new DateTime(2020, 2, 1)),
            (MediaTypeValues.Movie, "Film 3", new DateTime(2020, 3, 1)),
            (MediaTypeValues.Movie, "Film 4", new DateTime(2020, 4, 1)),
            (MediaTypeValues.Movie, "Film 5", new DateTime(2020, 5, 1)));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 2, 3, ct);

        Assert.Equal(2, result.Entries.Length);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public async Task GetEntriesPaged_TotalCountIsAccurate()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Vorhandener Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movieId),
            (MediaTypeValues.Movie, 999999));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Equal(1, result.TotalCount);
        Assert.False(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaId == 999999, ct));
    }

    [Fact]
    public async Task GetEntriesPaged_SortsByReleaseDate_WhenSortModeByReleaseDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithReleaseDatesAsync(_testUserId,
            (MediaTypeValues.Movie, "Zuletzt", new DateTime(2022, 1, 1)),
            (MediaTypeValues.Movie, "Zuerst", new DateTime(2020, 1, 1)),
            (MediaTypeValues.Movie, "Mitte", new DateTime(2021, 1, 1)));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Equal(new[] { "Zuerst", "Mitte", "Zuletzt" }, result.Entries.Select(e => e.MediaTitle));
    }

    [Fact]
    public async Task GetEntriesPaged_SortsWithFallbackToHierarchy_WhenReleaseDateMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null), (2, (DateTime?)null) }));
        var episodes = await _db.TVShowEpisodes.AsNoTracking().OrderBy(e => e.Number).ToListAsync(ct);
        var episode1 = episodes[0];
        var episode2 = episodes[1];

        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episode2.Id),
            (MediaTypeValues.TVShowEpisode, episode1.Id));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Equal(new[] { episode1.Id, episode2.Id }, result.Entries.Select(e => e.MediaId));
    }

    [Fact]
    public async Task GetEntriesPaged_SortsWithFallbackToAddedAt_WhenDateAndHierarchyMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieAId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Spaeter hinzugefuegt");
        var movieBId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Zuerst hinzugefuegt");

        var playlist = new Playlist
        {
            UserId = _testUserId,
            Name = $"Test-Playlist-{Guid.NewGuid()}",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync(ct);

        var baseTime = DateTime.UtcNow;
        _db.PlaylistEntries.Add(new PlaylistEntry { PlaylistId = playlist.Id, MediaType = MediaTypeValues.Movie, MediaId = movieAId, AddedAt = baseTime.AddMinutes(10) });
        _db.PlaylistEntries.Add(new PlaylistEntry { PlaylistId = playlist.Id, MediaType = MediaTypeValues.Movie, MediaId = movieBId, AddedAt = baseTime });
        await _db.SaveChangesAsync(ct);

        var result = await _service.GetPlaylistEntriesPagedAsync(playlist.Id, _testUserId, 1, 20, ct);

        Assert.Equal(new[] { movieBId, movieAId }, result.Entries.Select(e => e.MediaId));
    }

    [Fact]
    public async Task GetEntriesPaged_SortsWithFallbackToHierarchy_GroupsByParentBeforeAddedAt_WhenSequenceNumbersTie()
    {
        var ct = TestContext.Current.CancellationToken;
        var showA = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db, ("Staffel 1", Array.Empty<(int, DateTime?)>()));
        var showB = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db, ("Staffel 1", Array.Empty<(int, DateTime?)>()));
        var seasonA = await _db.TVShowSeasons.AsNoTracking().FirstAsync(s => s.TVShowId == showA.Id, ct);
        var seasonB = await _db.TVShowSeasons.AsNoTracking().FirstAsync(s => s.TVShowId == showB.Id, ct);

        // Both seasons are "Staffel 1" of their respective show, so they carry the same relative
        // SequenceNumber (1). Show B's season is added to the playlist first, so a sort that fell
        // back straight from SequenceNumber to AddedAt would place it before show A's season.
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowSeason, seasonB.Id),
            (MediaTypeValues.TVShowSeason, seasonA.Id));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        // Grouping by ParentId (TVShowId) before SequenceNumber/AddedAt keeps entries of the
        // earlier-created show (A) together and ahead of show B's, regardless of playlist order.
        Assert.Equal(new[] { seasonA.Id, seasonB.Id }, result.Entries.Select(e => e.MediaId));
    }

    [Fact]
    public async Task GetEntriesPaged_SortsMixedMediaTypes_Correctly()
    {
        var ct = TestContext.Current.CancellationToken;
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)new DateTime(2021, 6, 1)) }));
        var episode = await _db.TVShowEpisodes.AsNoTracking().FirstAsync(ct);

        var playlistId = await CreateTestPlaylistWithReleaseDatesAsync(_testUserId,
            (MediaTypeValues.Movie, "Film Frueh", new DateTime(2020, 1, 1)),
            (MediaTypeValues.Movie, "Film Spaet", new DateTime(2022, 1, 1)));
        _db.PlaylistEntries.Add(new PlaylistEntry { PlaylistId = playlistId, MediaType = MediaTypeValues.TVShowEpisode, MediaId = episode.Id, AddedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Equal(new[] { "Film Frueh", episode.Name, "Film Spaet" }, result.Entries.Select(e => e.MediaTitle));
    }

    [Fact]
    public async Task GetEntriesPaged_EntryNotUnlocked_IsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Eine Serie");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Single(result.Entries);
        Assert.False(result.Entries[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntriesPaged_EntryUnlockedForCurrentUser_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Eine Serie");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, showId);

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Single(result.Entries);
        Assert.True(result.Entries[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntriesPaged_UserHasSourceAccess_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ein Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Single(result.Entries);
        Assert.True(result.Entries[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntriesPaged_UserUnlockedNoSourceAccess_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Eine Serie");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, showId);

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Single(result.Entries);
        Assert.True(result.Entries[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntriesPaged_UserNoAccessNoUnlock_IsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ein Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Single(result.Entries);
        Assert.False(result.Entries[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntriesPaged_ResolvesResolvedPictureId_FromMediaEntity()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ein Film");
        var poster = new Picture { Type = "poster", Data = [0x1], ContentType = "image/png" };
        _db.Pictures.Add(poster);
        await _db.SaveChangesAsync(ct);
        var movie = await _db.Movies.FirstAsync(m => m.Id == movieId, ct);
        movie.PosterPictureId = poster.Id;
        await _db.SaveChangesAsync(ct);

        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Single(result.Entries);
        Assert.Equal(poster.Id, result.Entries[0].ResolvedPictureId);
    }

    [Fact]
    public async Task GetEntriesPaged_NoPictureSet_ResolvedPictureIdIsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ein Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Single(result.Entries);
        Assert.Null(result.Entries[0].ResolvedPictureId);
    }

    [Fact]
    public async Task GetEntriesPaged_NotOwner_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.GetPlaylistEntriesPagedAsync(playlistId, _otherUserId, 1, 20, ct));
    }

    [Fact]
    public async Task GetEntriesPaged_PlaylistNotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetPlaylistEntriesPagedAsync(999999, _testUserId, 1, 20, ct));
    }

    [Fact]
    public async Task GetEntriesPaged_EmptyPlaylist_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        var result = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        Assert.Empty(result.Entries);
        Assert.Equal(0, result.TotalCount);
        Assert.False(result.HasNextPage);
    }
}
