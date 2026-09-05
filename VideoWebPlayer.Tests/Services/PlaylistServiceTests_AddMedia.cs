using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.AddMediaToPlaylistAsync"/>.
/// </summary>
public class PlaylistServiceTests_AddMedia : PlaylistServiceTestBase
{
    [Fact]
    public async Task AddMedia_ValidMovie_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Mein Film");

        var dto = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct);

        Assert.Equal(playlistId, dto.PlaylistId);
        Assert.Equal(MediaTypeValues.Movie, dto.MediaType);
        Assert.Equal(movieId, dto.MediaId);
        Assert.Equal("Mein Film", dto.MediaTitle);
        Assert.Null(dto.ParentMediaType);
        Assert.Null(dto.ParentMediaId);
    }

    [Fact]
    public async Task AddMedia_Duplicate_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct));

        Assert.Equal("Medieninhalt bereits in dieser Playlist vorhanden.", ex.Message);
    }

    [Fact]
    public async Task AddMedia_InvalidMediaType_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddMediaToPlaylistAsync(playlistId, _testUserId, "UnknownType", 1, ct));

        Assert.Equal("Ungueltiger Medientyp.", ex.Message);
    }

    [Fact]
    public async Task AddMedia_MediaNotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, 999999, ct));
    }

    [Fact]
    public async Task AddMedia_NotOwner_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.AddMediaToPlaylistAsync(playlistId, _otherUserId, MediaTypeValues.Movie, movieId, ct));
    }

    [Fact]
    public async Task AddMedia_TVShow_CascadesEpisodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null), (2, (DateTime?)null) }),
            ("Staffel 2", new[] { (1, (DateTime?)null) }));

        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, show.Id, ct);

        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        Assert.Equal(1 + 2 + 3, entries.Count);
        Assert.Single(entries, e => e.MediaType == MediaTypeValues.TVShow && e.MediaId == show.Id && e.ParentMediaType == null);
        Assert.Equal(2, entries.Count(e => e.MediaType == MediaTypeValues.TVShowSeason));
        Assert.Equal(3, entries.Count(e => e.MediaType == MediaTypeValues.TVShowEpisode));
        Assert.All(entries.Where(e => e.MediaType != MediaTypeValues.TVShow), e =>
        {
            Assert.Equal(MediaTypeValues.TVShow, e.ParentMediaType);
            Assert.Equal(show.Id, e.ParentMediaId);
        });
    }

    [Fact]
    public async Task AddMedia_TVShowSeason_CascadesEpisodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null), (2, (DateTime?)null) }));
        var seasonId = await _db.TVShowSeasons.AsNoTracking().Where(s => s.TVShowId == show.Id).Select(s => s.Id).SingleAsync(ct);

        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShowSeason, seasonId, ct);

        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        Assert.Equal(1 + 2, entries.Count);
        Assert.Equal(2, entries.Count(e => e.MediaType == MediaTypeValues.TVShowEpisode
            && e.ParentMediaType == MediaTypeValues.TVShowSeason && e.ParentMediaId == seasonId));
    }

    [Fact]
    public async Task AddMedia_MovieCollection_CascadesMovies()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var movie1 = new Data.Movie { Name = "Film 1", MediaSourceId = 1, MovieCollectionId = collectionId, CreatedAt = DateTime.UtcNow };
        var movie2 = new Data.Movie { Name = "Film 2", MediaSourceId = 1, MovieCollectionId = collectionId, CreatedAt = DateTime.UtcNow };
        _db.Movies.AddRange(movie1, movie2);
        await _db.SaveChangesAsync(ct);

        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.MovieCollection, collectionId, ct);

        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        Assert.Equal(1 + 2, entries.Count);
        Assert.Equal(2, entries.Count(e => e.MediaType == MediaTypeValues.Movie
            && e.ParentMediaType == MediaTypeValues.MovieCollection && e.ParentMediaId == collectionId));
    }

    [Fact]
    public async Task AddMedia_CascadeWithDuplicates_SkipsDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null), (2, (DateTime?)null) }));
        var episodeId = await _db.TVShowEpisodes.AsNoTracking().Select(e => e.Id).FirstAsync(ct);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, episodeId));

        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, show.Id, ct);

        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        // 1 pre-existing episode + 1 show + 1 season + 1 new episode = 4 (the duplicate episode is skipped)
        Assert.Equal(4, entries.Count);
        Assert.Single(entries, e => e.MediaType == MediaTypeValues.TVShowEpisode && e.MediaId == episodeId);
    }

    [Fact]
    public async Task AddMedia_MaxItemCountExceeded_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var limitedService = CreateService(maxPlaylistItemCount: 1);
        var existingMovieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Bereits vorhanden");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, existingMovieId));
        var newMovieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Neuer Film");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => limitedService.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, newMovieId, ct));

        Assert.Equal("Die maximale Anzahl an Playlist-Eintraegen wurde erreicht.", ex.Message);
        Assert.False(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == newMovieId, ct));
    }

    [Fact]
    public async Task AddMedia_CascadeExceedsMaxItemCount_ThrowsInvalidOperationExceptionWithoutPartialInsert()
    {
        var ct = TestContext.Current.CancellationToken;
        var limitedService = CreateService(maxPlaylistItemCount: 2);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null), (2, (DateTime?)null) }));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => limitedService.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, show.Id, ct));

        Assert.Empty(await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct));
    }
}
