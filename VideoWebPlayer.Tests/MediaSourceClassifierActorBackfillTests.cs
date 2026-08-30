using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

[Collection(MediaSourceClassifierCollection.Name)]
public sealed class MediaSourceClassifierActorBackfillTests
{
    [Fact]
    public async Task BackfillMissingActorsAsync_LoadsActors_FromMovieDotNfo()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:actor-backfill-movie?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source = new MediaSource
        {
            Name = "Test",
            Path = "/media",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);

        var collection = new MediaCollection
        {
            Name = "Movies",
            Path = "/media/movies",
            MediaSource = source,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);

        var mediaItem = new MediaItem
        {
            Name = "The Test",
            Path = "/media/movies/The Test.mkv",
            MediaCollection = collection,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);

        var movie = new Movie
        {
            Name = "The Test",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        db.MovieMediaItems.Add(new MovieMediaItem { Movie = movie, MediaItem = mediaItem });

        await db.SaveChangesAsync(ct);

        var nfo = @"<movie>
            <actor><name>John Doe</name></actor>
            <actor name='Jane Doe' thumb='jane.jpg' />
            <actor><nonsense /></actor>
        </movie>";

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/movies", "movie.nfo")] = nfo,
            [("/media/movies", "jane.jpg")] = "image-bytes-for-jane"
        });

        var classifier = new MediaSourceClassifier(
            db,
            reader,
            null!,
            new EventManager(),
            null!,
            NullLogger<MediaSourceClassifier>.Instance,
            new HttpClient());

        await classifier.BackfillMissingActorsAsync(ct);

        var reloaded = await db.Movies
            .Include(m => m.MovieActors)
            .ThenInclude(ma => ma.Actor)
            .FirstAsync(m => m.Id == movie.Id, ct);

        Assert.NotNull(reloaded.ActorsClassifiedAt);
        Assert.Equal(2, reloaded.MovieActors.Count);
        Assert.Contains(reloaded.MovieActors, ma => ma.Actor!.Name == "John Doe");
        Assert.Contains(reloaded.MovieActors, ma => ma.Actor!.Name == "Jane Doe");

        var jane = await db.Actors.FirstAsync(a => a.Name == "Jane Doe", ct);
        Assert.True(jane.PictureId.HasValue);
    }

    [Fact]
    public async Task BackfillMissingActorsAsync_LoadsActors_FromTvShowNfoInParentCollection()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:actor-backfill-tvshow?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source = new MediaSource
        {
            Name = "Test",
            Path = "/media",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);

        var showCollection = new MediaCollection
        {
            Name = "Show",
            Path = "/media/show",
            MediaSource = source,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(showCollection);

        var seasonCollection = new MediaCollection
        {
            Name = "Season 1",
            Path = "/media/show/Season 1",
            MediaSource = source,
            ParentMediaCollection = showCollection,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(seasonCollection);

        var mediaItem = new MediaItem
        {
            Name = "S01E01",
            Path = "/media/show/Season 1/S01E01.mkv",
            MediaCollection = seasonCollection,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);

        var tvShow = new TVShow
        {
            Name = "Show",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShows.Add(tvShow);

        var season = new TVShowSeason
        {
            TVShow = tvShow,
            Name = "Season 1",
            CreatedAt = DateTime.UtcNow
        };
        db.TVShowSeasons.Add(season);

        var episode = new TVShowEpisode
        {
            Name = "Pilot",
            Number = 1,
            TVShowSeason = season,
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShowEpisodes.Add(episode);
        db.TVShowEpisodeMediaItems.Add(new TVShowEpisodeMediaItem { TVShowEpisode = episode, MediaItem = mediaItem });

        await db.SaveChangesAsync(ct);

        var nfo = @"<tvshow>
            <actor><name>Show Lead</name></actor>
        </tvshow>";

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/show", "tvshow.nfo")] = nfo
        });

        var classifier = new MediaSourceClassifier(
            db,
            reader,
            null!,
            new EventManager(),
            null!,
            NullLogger<MediaSourceClassifier>.Instance,
            new HttpClient());

        await classifier.BackfillMissingActorsAsync(ct);

        var reloaded = await db.TVShowEpisodes
            .Include(e => e.TVShowEpisodeActors)
            .ThenInclude(ea => ea.Actor)
            .FirstAsync(e => e.Id == episode.Id, ct);

        Assert.NotNull(reloaded.ActorsClassifiedAt);
        Assert.Single(reloaded.TVShowEpisodeActors);
        Assert.Equal("Show Lead", reloaded.TVShowEpisodeActors.First().Actor!.Name);
    }

    [Fact]
    public async Task BackfillMissingActorsAsync_SkipsManuallyEditedMovie()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:actor-backfill-manual?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);

        var source = new MediaSource
        {
            Name = "Test",
            Path = "/media",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);

        var collection = new MediaCollection
        {
            Name = "Movies",
            Path = "/media/movies",
            MediaSource = source,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);

        var mediaItem = new MediaItem
        {
            Name = "Manual",
            Path = "/media/movies/Manual.mkv",
            MediaCollection = collection,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);

        var movie = new Movie
        {
            Name = "Manual",
            MediaSourceId = source.Id,
            IsManuallyEdited = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        db.MovieMediaItems.Add(new MovieMediaItem { Movie = movie, MediaItem = mediaItem });

        await db.SaveChangesAsync(ct);

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/movies", "movie.nfo")] = "<movie><actor><name>Actor</name></actor></movie>"
        });

        var classifier = new MediaSourceClassifier(
            db,
            reader,
            null!,
            new EventManager(),
            null!,
            NullLogger<MediaSourceClassifier>.Instance,
            new HttpClient());

        await classifier.BackfillMissingActorsAsync(ct);

        var reloaded = await db.Movies
            .Include(m => m.MovieActors)
            .FirstAsync(m => m.Id == movie.Id, ct);

        Assert.Null(reloaded.ActorsClassifiedAt);
        Assert.Empty(reloaded.MovieActors);
    }
}
