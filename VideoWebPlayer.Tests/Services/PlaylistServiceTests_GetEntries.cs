using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.GetPlaylistEntriesAsync"/>.
/// </summary>
public class PlaylistServiceTests_GetEntries : PlaylistServiceTestBase
{
    [Fact]
    public async Task GetEntries_ReturnsAllEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Mein Film");
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Meine Serie");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movieId),
            (MediaTypeValues.TVShow, showId));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Equal(2, result.Length);
        Assert.Contains(result, e => e.MediaType == MediaTypeValues.Movie && e.MediaId == movieId && e.MediaTitle == "Mein Film");
        Assert.Contains(result, e => e.MediaType == MediaTypeValues.TVShow && e.MediaId == showId && e.MediaTitle == "Meine Serie");
    }

    [Fact]
    public async Task GetEntries_EmptyPlaylist_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEntries_NotOwner_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.GetPlaylistEntriesAsync(playlistId, _otherUserId, ct));
    }

    [Fact]
    public async Task GetEntries_PlaylistNotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetPlaylistEntriesAsync(999999, _testUserId, ct));
    }

    [Fact]
    public async Task GetEntries_RemovesOrphanedEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Vorhandener Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movieId),
            (MediaTypeValues.Movie, 999999));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.Equal(movieId, result[0].MediaId);
    }

    [Fact]
    public async Task GetEntries_OrphanedEntry_DeletedFromDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 999999));

        await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.False(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == 999999, ct));
    }

    [Fact]
    public async Task GetEntries_CascadedEntry_ParentMediaTitleIsLoaded()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null) }));
        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, show.Id, ct);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        var seasonEntry = Assert.Single(result, e => e.MediaType == MediaTypeValues.TVShowSeason);
        Assert.Equal(MediaTypeValues.TVShow, seasonEntry.ParentMediaType);
        Assert.Equal(show.Id, seasonEntry.ParentMediaId);
        Assert.Equal(show.Name, seasonEntry.ParentMediaTitle);

        var episodeEntry = Assert.Single(result, e => e.MediaType == MediaTypeValues.TVShowEpisode);
        Assert.Equal(MediaTypeValues.TVShow, episodeEntry.ParentMediaType);
        Assert.Equal(show.Id, episodeEntry.ParentMediaId);
        Assert.Equal(show.Name, episodeEntry.ParentMediaTitle);
    }

    [Fact]
    public async Task GetEntries_EntryNotUnlocked_IsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Eine Serie");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.False(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_EntryUnlockedForCurrentUser_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Eine Serie");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, showId);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.True(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserHasSourceAccess_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Serie mit Quellenzugriff");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.True(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserHasSourceAccess_MovieType_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ein Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.True(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserHasSourceAccess_EpisodeType_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var episodeId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShowEpisode, "Eine Episode");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, episodeId));
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.True(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserHasSourceAccess_SeasonType_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var seasonId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShowSeason, "Eine Staffel");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowSeason, seasonId));
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.True(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserUnlockedNoSourceAccess_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Nur freigeschaltet");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, showId);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.True(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserUnlockedNoSourceAccess_MovieType_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var movie = new Movie { Name = "Film", MediaSourceId = 1, MovieCollectionId = collectionId, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync(ct);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movie.Id));
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.MovieCollection, collectionId);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.True(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserUnlockedNoSourceAccess_EpisodeType_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null) }));
        var episode = await _db.TVShowEpisodes.AsNoTracking().FirstAsync(ct);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, episode.Id));
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, show.Id);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.True(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserUnlockedNoSourceAccess_SeasonType_IsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", Array.Empty<(int, DateTime?)>()));
        var season = await _db.TVShowSeasons.AsNoTracking().FirstAsync(s => s.TVShowId == show.Id, ct);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowSeason, season.Id));
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, show.Id);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.True(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserNoAccessNoUnlock_IsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Keine Berechtigung");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.False(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserNoAccessNoUnlock_MovieType_IsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ein Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.False(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserNoAccessNoUnlock_EpisodeType_IsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var episodeId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShowEpisode, "Eine Episode");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, episodeId));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.False(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserNoAccessNoUnlock_SeasonType_IsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var seasonId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShowSeason, "Eine Staffel");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowSeason, seasonId));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.False(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_MovieCollectionIdCollidesWithUnlockedTVShowId_MovieIsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;

        // In a fresh test database both tables assign id 1 to their first row, so a TV show and a
        // movie collection created here collide on id 1 despite living in independent id spaces.
        var show = new TVShow { Name = "Freigeschaltete Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.TVShows.Add(show);
        await _db.SaveChangesAsync(ct);
        await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, show.Id);

        var collection = new MovieCollection { Name = "Nicht freigeschaltete Sammlung", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.MovieCollections.Add(collection);
        await _db.SaveChangesAsync(ct);
        Assert.Equal(show.Id, collection.Id);

        var movie = new Movie { Name = "Film aus nicht freigeschalteter Sammlung", MediaSourceId = 1, MovieCollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync(ct);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movie.Id));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.False(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_UserNoAccessNoUnlock_CollectionType_IsNotAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.MovieCollection, collectionId));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.False(result[0].IsAccessible);
    }

    [Fact]
    public async Task GetEntries_ResolvesResolvedPictureId_FromMediaEntity()
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

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.Equal(poster.Id, result[0].ResolvedPictureId);
    }

    [Fact]
    public async Task GetEntries_NoPictureSet_ResolvedPictureIdIsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ein Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.Null(result[0].ResolvedPictureId);
    }
}
