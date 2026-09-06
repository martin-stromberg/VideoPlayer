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

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct);

        Assert.NotNull(result.TopLevelEntry);
        Assert.Equal(playlistId, result.TopLevelEntry!.PlaylistId);
        Assert.Equal(MediaTypeValues.Movie, result.TopLevelEntry.MediaType);
        Assert.Equal(movieId, result.TopLevelEntry.MediaId);
        Assert.Equal("Mein Film", result.TopLevelEntry.MediaTitle);
        Assert.Null(result.TopLevelEntry.ParentMediaType);
        Assert.Null(result.TopLevelEntry.ParentMediaId);
        Assert.Single(result.AddedEntries);
        Assert.Equal(0, result.SkippedDuplicateCount);
        Assert.Equal("1 Titel hinzugefuegt.", result.Message);
    }

    [Fact]
    public async Task AddMedia_TopLevelDuplicate_SkipsAndReturnsCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct);

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct);

        Assert.Null(result.TopLevelEntry);
        Assert.Empty(result.AddedEntries);
        Assert.Equal(1, result.SkippedDuplicateCount);
        Assert.Equal("Alle 1 Titel waren bereits vorhanden.", result.Message);
    }

    [Fact]
    public async Task AddMedia_PartialDuplicates_AddedNewAndSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null), (2, (DateTime?)null), (3, (DateTime?)null) }));
        var existingEpisodeId = await _db.TVShowEpisodes.AsNoTracking().Select(e => e.Id).FirstAsync(ct);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, existingEpisodeId));

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, show.Id, ct);

        // Show + Season + 2 new episodes = 4 added; 1 episode already existed and is skipped.
        Assert.Equal(4, result.AddedEntries.Length);
        Assert.Equal(1, result.SkippedDuplicateCount);
        Assert.Equal("4 Titel hinzugefuegt, 1 bereits vorhanden und uebersprungen.", result.Message);
    }

    [Fact]
    public async Task AddMedia_AllDuplicates_ReturnsZeroAddedCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null), (2, (DateTime?)null) }),
            ("Staffel 2", new[] { (1, (DateTime?)null) }));
        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, show.Id, ct);

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, show.Id, ct);

        Assert.Null(result.TopLevelEntry);
        Assert.Empty(result.AddedEntries);
        Assert.Equal(6, result.SkippedDuplicateCount);
        Assert.Equal("Alle 6 Titel waren bereits vorhanden.", result.Message);
    }

    [Fact]
    public async Task AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, "Movie", movieId, ct);

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, "movie", movieId, ct);

        Assert.Equal(1, result.SkippedDuplicateCount);
        Assert.Empty(result.AddedEntries);
        var storedEntry = await _db.PlaylistEntries.AsNoTracking().SingleAsync(e => e.PlaylistId == playlistId, ct);
        Assert.Equal("Movie", storedEntry.MediaType);
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

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, show.Id, ct);

        var entries = await _db.PlaylistEntries.AsNoTracking().Where(e => e.PlaylistId == playlistId).ToListAsync(ct);
        // 1 pre-existing episode + 1 show + 1 season + 1 new episode = 4 (the duplicate episode is skipped)
        Assert.Equal(4, entries.Count);
        Assert.Single(entries, e => e.MediaType == MediaTypeValues.TVShowEpisode && e.MediaId == episodeId);
        Assert.Equal(1, result.SkippedDuplicateCount);
        Assert.Equal(3, result.AddedEntries.Length);
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

    [Fact]
    public async Task AddMedia_TVShowNotUnlocked_TopLevelEntryIsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Eine Serie");

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, showId, ct);

        Assert.NotNull(result.TopLevelEntry);
        Assert.False(result.TopLevelEntry!.IsAccessible);
        Assert.Single(result.AddedEntries, e => e.MediaType == MediaTypeValues.TVShow && e.MediaId == showId);
        Assert.False(result.AddedEntries.Single(e => e.MediaType == MediaTypeValues.TVShow).IsAccessible);
    }

    [Fact]
    public async Task AddMedia_TVShowUnlockedForCurrentUser_TopLevelEntryIsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Eine Serie");
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, showId);

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, showId, ct);

        Assert.NotNull(result.TopLevelEntry);
        Assert.True(result.TopLevelEntry!.IsAccessible);
    }

    [Fact]
    public async Task AddMedia_ResolvesResolvedPictureId_FromMediaEntity()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ein Film");
        var poster = new Data.Picture { Type = "poster", Data = [0x1], ContentType = "image/png" };
        _db.Pictures.Add(poster);
        await _db.SaveChangesAsync(ct);
        var movie = await _db.Movies.FirstAsync(m => m.Id == movieId, ct);
        movie.PosterPictureId = poster.Id;
        await _db.SaveChangesAsync(ct);

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct);

        Assert.NotNull(result.TopLevelEntry);
        Assert.Equal(poster.Id, result.TopLevelEntry!.ResolvedPictureId);
    }

    [Fact]
    public async Task AddMedia_NoPictureSet_ResolvedPictureIdIsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ein Film");

        var result = await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct);

        Assert.NotNull(result.TopLevelEntry);
        Assert.Null(result.TopLevelEntry!.ResolvedPictureId);
    }
}
