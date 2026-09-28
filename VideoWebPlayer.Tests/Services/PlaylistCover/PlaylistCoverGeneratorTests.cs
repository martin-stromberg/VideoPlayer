using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.PlaylistCover;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services.PlaylistCover;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistCover.PlaylistCoverImageGenerator"/>: the
/// media-type priority order used to collect source images for a playlist cover collage
/// (Entwicklungsschritt 10, Playlist-Abbildungen), including the TVShowSeason fallback to its parent
/// TVShow's poster, and the "no images" / "fewer than max images" edge cases.
/// </summary>
public class PlaylistCoverGeneratorTests : PlaylistServiceTestBase
{
    [Fact]
    public async Task GeneratePlaylistCover_TVShowAndMovie_TVShowFirst()
    {
        var movieId = await CreateMovieWithPosterAsync("Film", CreateJpegBytes(SixLabors.ImageSharp.Color.Blue));
        var showId = await CreateTVShowWithPosterAsync("Serie", CreateJpegBytes(SixLabors.ImageSharp.Color.Red));

        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movieId),
            (MediaTypeValues.TVShow, showId));

        var generator = CreateGenerator();
        var orderedPictureIds = await generator.CollectOrderedPictureIdsAsync(playlistId, TestContext.Current.CancellationToken);

        var showPictureId = (await _db.TVShows.FindAsync(new object[] { showId }, TestContext.Current.CancellationToken))!.PosterPictureId!.Value;
        var moviePictureId = (await _db.Movies.FindAsync(new object[] { movieId }, TestContext.Current.CancellationToken))!.PosterPictureId!.Value;
        Assert.Equal(new[] { showPictureId, moviePictureId }, orderedPictureIds);
    }

    [Fact]
    public async Task GeneratePlaylistCover_NoImages_ReturnsNull()
    {
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film ohne Bild");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var generator = CreateGenerator();
        var collage = await generator.GeneratePlaylistCoverAsync(playlistId, TestContext.Current.CancellationToken);

        Assert.Null(collage);
    }

    [Fact]
    public async Task GeneratePlaylistCover_LessThanFiveImages_UsesAllAvailable()
    {
        var movie1 = await CreateMovieWithPosterAsync("Film 1", CreateJpegBytes(SixLabors.ImageSharp.Color.Blue));
        var movie2 = await CreateMovieWithPosterAsync("Film 2", CreateJpegBytes(SixLabors.ImageSharp.Color.Green));
        var movie3 = await CreateMovieWithPosterAsync("Film 3", CreateJpegBytes(SixLabors.ImageSharp.Color.Yellow));

        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movie1),
            (MediaTypeValues.Movie, movie2),
            (MediaTypeValues.Movie, movie3));

        var generator = CreateGenerator();
        var orderedPictureIds = await generator.CollectOrderedPictureIdsAsync(playlistId, TestContext.Current.CancellationToken);

        Assert.Equal(3, orderedPictureIds.Count);
    }

    /// <summary>
    /// Test 4 (plan.md): Movie, TVShow, TVShowEpisode, MovieCollection, Movie, Movie (6 entries) → exactly
    /// 5 images, in priority order TVShow, TVShowEpisode, MovieCollection, then the two Movies in the order
    /// they were added (the first Movie added, then the last one) - the sixth entry (third Movie) is cut off.
    /// </summary>
    [Fact]
    public async Task GeneratePlaylistCover_CompletePriorityOrder_MaxFiveImages()
    {
        var movie1 = await CreateMovieWithPosterAsync("Film 1", CreateJpegBytes(SixLabors.ImageSharp.Color.Blue));
        var showId = await CreateTVShowWithPosterAsync("Serie", CreateJpegBytes(SixLabors.ImageSharp.Color.Red));
        var episodeId = await CreateEpisodeWithPosterAsync("Episode", CreateJpegBytes(SixLabors.ImageSharp.Color.Purple));
        var collectionId = await CreateMovieCollectionWithPosterAsync("Sammlung", CreateJpegBytes(SixLabors.ImageSharp.Color.Orange));
        var movie2 = await CreateMovieWithPosterAsync("Film 2", CreateJpegBytes(SixLabors.ImageSharp.Color.Green));
        var movie3 = await CreateMovieWithPosterAsync("Film 3", CreateJpegBytes(SixLabors.ImageSharp.Color.Yellow));

        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movie1),
            (MediaTypeValues.TVShow, showId),
            (MediaTypeValues.TVShowEpisode, episodeId),
            (MediaTypeValues.MovieCollection, collectionId),
            (MediaTypeValues.Movie, movie2),
            (MediaTypeValues.Movie, movie3));

        var generator = CreateGenerator();
        var orderedPictureIds = await generator.CollectOrderedPictureIdsAsync(playlistId, TestContext.Current.CancellationToken);

        var showPictureId = (await _db.TVShows.FindAsync(new object[] { showId }, TestContext.Current.CancellationToken))!.PosterPictureId!.Value;
        var episodePictureId = (await _db.TVShowEpisodes.FindAsync(new object[] { episodeId }, TestContext.Current.CancellationToken))!.PosterPictureId!.Value;
        var collectionPictureId = (await _db.MovieCollections.FindAsync(new object[] { collectionId }, TestContext.Current.CancellationToken))!.PosterPictureId!.Value;
        var movie1PictureId = (await _db.Movies.FindAsync(new object[] { movie1 }, TestContext.Current.CancellationToken))!.PosterPictureId!.Value;
        var movie2PictureId = (await _db.Movies.FindAsync(new object[] { movie2 }, TestContext.Current.CancellationToken))!.PosterPictureId!.Value;

        Assert.Equal(5, orderedPictureIds.Count);
        Assert.Equal(new[] { showPictureId, episodePictureId, collectionPictureId, movie1PictureId, movie2PictureId }, orderedPictureIds);
    }

    [Fact]
    public async Task GeneratePlaylistCover_TVShowSeasonFallback_UsesParentTVShowPoster()
    {
        var show = new TVShow { Name = "Serie mit Staffel", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.TVShows.Add(show);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var showPictureId = await CreatePictureAsync(CreateJpegBytes(SixLabors.ImageSharp.Color.Red));
        show.PosterPictureId = showPictureId;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var season = new TVShowSeason { Name = "Staffel 1", TVShowId = show.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.TVShowSeasons.Add(season);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowSeason, season.Id));

        var generator = CreateGenerator();
        var orderedPictureIds = await generator.CollectOrderedPictureIdsAsync(playlistId, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { showPictureId }, orderedPictureIds);
    }

    private PlaylistCoverImageGenerator CreateGenerator(int width = 1600, int height = 520, int quality = 85)
    {
        var settings = Options.Create(new PlaylistSettings
        {
            GeneratedCoverWidthPixels = width,
            GeneratedCoverHeightPixels = height,
            GeneratedCoverJpegQuality = quality
        });
        return new PlaylistCoverImageGenerator(_db, settings, NullLogger<VideoWebPlayer.Services.PlaylistCover.PlaylistCoverImageGenerator>.Instance);
    }

    private async Task<long> CreatePictureAsync(byte[] data)
    {
        var picture = new Picture { Type = "poster", Data = data, ContentType = "image/jpeg" };
        _db.Pictures.Add(picture);
        await _db.SaveChangesAsync();
        return picture.Id;
    }

    private async Task<long> CreateMovieWithPosterAsync(string name, byte[] posterBytes)
    {
        var pictureId = await CreatePictureAsync(posterBytes);
        var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, PosterPictureId = pictureId };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie.Id;
    }

    private async Task<long> CreateTVShowWithPosterAsync(string name, byte[] posterBytes)
    {
        var pictureId = await CreatePictureAsync(posterBytes);
        var show = new TVShow { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, PosterPictureId = pictureId };
        _db.TVShows.Add(show);
        await _db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<long> CreateMovieCollectionWithPosterAsync(string name, byte[] posterBytes)
    {
        var pictureId = await CreatePictureAsync(posterBytes);
        var collection = new MovieCollection { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, PosterPictureId = pictureId };
        _db.MovieCollections.Add(collection);
        await _db.SaveChangesAsync();
        return collection.Id;
    }

    private async Task<long> CreateEpisodeWithPosterAsync(string name, byte[] posterBytes)
    {
        var pictureId = await CreatePictureAsync(posterBytes);
        var show = new TVShow { Name = $"{name} Show", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.TVShows.Add(show);
        await _db.SaveChangesAsync();
        var season = new TVShowSeason { Name = $"{name} Season", TVShowId = show.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.TVShowSeasons.Add(season);
        await _db.SaveChangesAsync();
        var episode = new TVShowEpisode { Name = name, TVShowSeasonId = season.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, PosterPictureId = pictureId };
        _db.TVShowEpisodes.Add(episode);
        await _db.SaveChangesAsync();
        return episode.Id;
    }

    private static byte[] CreateJpegBytes(SixLabors.ImageSharp.Color color)
    {
        using var image = new Image<Rgba32>(8, 8, color.ToPixel<Rgba32>());
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }
}
