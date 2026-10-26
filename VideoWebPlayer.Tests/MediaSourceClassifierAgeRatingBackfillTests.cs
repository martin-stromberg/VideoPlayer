using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

[Collection(MediaSourceClassifierCollection.Name)]
public sealed class MediaSourceClassifierAgeRatingBackfillTests
{
    [Fact]
    public async Task BackfillMissingAgeRatingsAsync_FillsFromExistingNfo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var testDb = await CreateDbAsync("agerating-backfill-fill", ct);
        var db = testDb.Context;
        var (movie, _) = await SeedMovieAsync(db, "The Test", "/media/movies/The Test.mkv", ct);

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/movies", "The Test.nfo")] = "<movie><title>The Test</title><mpaa>FSK 12</mpaa></movie>"
        });

        await CreateClassifier(db, reader).BackfillMissingAgeRatingsAsync(ct);

        var reloaded = await db.Movies.FirstAsync(m => m.Id == movie.Id, ct);
        Assert.Equal("FSK 12", reloaded.AgeRatingRaw);
        Assert.Equal("FSK 12", reloaded.AgeRating);
        Assert.Equal(12, reloaded.AgeRatingMinAge);
        Assert.NotNull(reloaded.AgeRatingClassifiedAt);
    }

    [Fact]
    public async Task BackfillMissingAgeRatingsAsync_FillsTvShowFromTvShowNfo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var testDb = await CreateDbAsync("agerating-backfill-tvshow", ct);
        var db = testDb.Context;
        var source = await SeedSourceAsync(db, ct);
        var collection = new MediaCollection
        {
            Name = "Show",
            Path = "/media/show",
            MediaSource = source,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        var show = new TVShow
        {
            Name = "Show",
            MediaSourceId = source.Id,
            CollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShows.Add(show);
        await db.SaveChangesAsync(ct);

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/show", "tvshow.nfo")] = "<tvshow><title>Show</title><mpaa>Rated PG-13</mpaa></tvshow>"
        });

        await CreateClassifier(db, reader).BackfillMissingAgeRatingsAsync(ct);

        var reloaded = await db.TVShows.FirstAsync(s => s.Id == show.Id, ct);
        Assert.Equal("Rated PG-13", reloaded.AgeRatingRaw);
        Assert.Equal("PG-13", reloaded.AgeRating);
        Assert.Equal(13, reloaded.AgeRatingMinAge);
        Assert.NotNull(reloaded.AgeRatingClassifiedAt);
    }

    [Fact]
    public async Task BackfillMissingAgeRatingsAsync_SkipsManuallyEdited()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var testDb = await CreateDbAsync("agerating-backfill-manual", ct);
        var db = testDb.Context;
        var (movie, _) = await SeedMovieAsync(db, "Manual", "/media/movies/Manual.mkv", ct, m => m.IsManuallyEdited = true);

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/movies", "Manual.nfo")] = "<movie><title>Manual</title><mpaa>FSK 18</mpaa></movie>"
        });

        await CreateClassifier(db, reader).BackfillMissingAgeRatingsAsync(ct);

        var reloaded = await db.Movies.FirstAsync(m => m.Id == movie.Id, ct);
        Assert.Null(reloaded.AgeRating);
        Assert.Null(reloaded.AgeRatingRaw);
        Assert.Null(reloaded.AgeRatingClassifiedAt);
    }

    [Fact]
    public async Task BackfillMissingAgeRatingsAsync_SkipsAlreadyClassified()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var testDb = await CreateDbAsync("agerating-backfill-done", ct);
        var classifiedAt = DateTime.UtcNow.AddDays(-1);
        var db = testDb.Context;
        var (movie, _) = await SeedMovieAsync(db, "Done", "/media/movies/Done.mkv", ct,
            m =>
            {
                m.AgeRating = "FSK 6";
                m.AgeRatingMinAge = 6;
                m.AgeRatingClassifiedAt = classifiedAt;
            });

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/movies", "Done.nfo")] = "<movie><title>Done</title><mpaa>FSK 18</mpaa></movie>"
        });

        await CreateClassifier(db, reader).BackfillMissingAgeRatingsAsync(ct);

        var reloaded = await db.Movies.FirstAsync(m => m.Id == movie.Id, ct);
        Assert.Equal("FSK 6", reloaded.AgeRating);
        Assert.Equal(6, reloaded.AgeRatingMinAge);
        Assert.Equal(classifiedAt, reloaded.AgeRatingClassifiedAt);
    }

    [Fact]
    public async Task BackfillMissingAgeRatingsAsync_MovieDotNfoFallback()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var testDb = await CreateDbAsync("agerating-backfill-movienfo", ct);
        var db = testDb.Context;
        var (movie, _) = await SeedMovieAsync(db, "Fallback", "/media/movies/Fallback.mkv", ct);

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/movies", "movie.nfo")] = "<movie><title>Fallback</title><certification>US:R</certification></movie>"
        });

        await CreateClassifier(db, reader).BackfillMissingAgeRatingsAsync(ct);

        var reloaded = await db.Movies.FirstAsync(m => m.Id == movie.Id, ct);
        Assert.Equal("US:R", reloaded.AgeRatingRaw);
        Assert.Equal("R", reloaded.AgeRating);
        Assert.Equal(17, reloaded.AgeRatingMinAge);
        Assert.NotNull(reloaded.AgeRatingClassifiedAt);
    }

    [Fact]
    public async Task BackfillMissingAgeRatingsAsync_BrokenNfo_StillMarksClassified()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var testDb = await CreateDbAsync("agerating-backfill-brokennfo", ct);
        var db = testDb.Context;
        var (movie, _) = await SeedMovieAsync(db, "Broken", "/media/movies/Broken.mkv", ct);

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/movies", "Broken.nfo")] = "<movie><title>Broken</title><mpaa>FSK 12"
        });

        await CreateClassifier(db, reader).BackfillMissingAgeRatingsAsync(ct);

        var reloaded = await db.Movies.FirstAsync(m => m.Id == movie.Id, ct);
        Assert.NotNull(reloaded.AgeRatingClassifiedAt);
    }

    [Fact]
    public async Task BackfillMissingAgeRatingsAsync_BrokenNfo_OnlineLookupStillRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var testDb = await CreateDbAsync("agerating-backfill-brokennfo-online", ct);
        var db = testDb.Context;
        var (movie, _) = await SeedMovieAsync(db, "Broken Online", "/media/movies/BrokenOnline.mkv", ct);

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/movies", "BrokenOnline.nfo")] = "<movie><title>Broken Online</title><mpaa>FSK 12"
        });
        var fake = new FakeTmdbAgeRatingService(movieRating: new TmdbAgeRating("DE", "16"));

        await CreateClassifier(db, reader, fake).BackfillMissingAgeRatingsAsync(ct);

        var reloaded = await db.Movies.FirstAsync(m => m.Id == movie.Id, ct);
        Assert.Equal(1, fake.MovieCalls);
        Assert.Equal("DE:16", reloaded.AgeRatingRaw);
        Assert.Equal("FSK 16", reloaded.AgeRating);
        Assert.Equal(16, reloaded.AgeRatingMinAge);
        Assert.NotNull(reloaded.AgeRatingClassifiedAt);
    }

    [Fact]
    public async Task BackfillMissingAgeRatingsAsync_BrokenTvShowNfo_StillMarksClassified()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var testDb = await CreateDbAsync("agerating-backfill-brokennfo-show", ct);
        var db = testDb.Context;
        var source = await SeedSourceAsync(db, ct);
        var collection = new MediaCollection
        {
            Name = "BrokenShow",
            Path = "/media/brokenshow",
            MediaSource = source,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        var show = new TVShow
        {
            Name = "BrokenShow",
            MediaSourceId = source.Id,
            CollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShows.Add(show);
        await db.SaveChangesAsync(ct);

        var reader = new BackfillSftpMediaSourceReader(new()
        {
            [("/media/brokenshow", "tvshow.nfo")] = "<tvshow><title>BrokenShow</title><mpaa>PG"
        });

        await CreateClassifier(db, reader).BackfillMissingAgeRatingsAsync(ct);

        var reloaded = await db.TVShows.FirstAsync(s => s.Id == show.Id, ct);
        Assert.NotNull(reloaded.AgeRatingClassifiedAt);
    }

    private static async Task<TestDb> CreateDbAsync(string name, CancellationToken ct)
    {
        var connectionString = $"Data Source=file:{name}?mode=memory&cache=shared";
        var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(ct);
        return new TestDb(db, keeper);
    }

    private sealed class TestDb : IAsyncDisposable
    {
        private readonly SqliteConnection _keeper;

        public TestDb(ApplicationDbContext context, SqliteConnection keeper)
        {
            Context = context;
            _keeper = keeper;
        }

        public ApplicationDbContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _keeper.DisposeAsync();
        }
    }

    private static async Task<MediaSource> SeedSourceAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var source = new MediaSource
        {
            Name = "Test",
            Path = "/media",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);
        return source;
    }

    private static async Task<(Movie Movie, MediaItem Item)> SeedMovieAsync(
        ApplicationDbContext db, string name, string path, CancellationToken ct, Action<Movie>? configure = null)
    {
        var source = await SeedSourceAsync(db, ct);
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
            Name = name,
            Path = path,
            MediaCollection = collection,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);

        var movie = new Movie
        {
            Name = name,
            MediaSourceId = source.Id,
            CollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow
        };
        configure?.Invoke(movie);
        db.Movies.Add(movie);
        db.MovieMediaItems.Add(new MovieMediaItem { Movie = movie, MediaItem = mediaItem });
        await db.SaveChangesAsync(ct);
        return (movie, mediaItem);
    }

    private static MediaSourceClassifier CreateClassifier(
        ApplicationDbContext db, IMediaSourceReader reader, ITmdbAgeRatingService? ageRatingService = null)
        => new(
            db,
            reader,
            null!,
            new EventManager(),
            null!,
            NullLogger<MediaSourceClassifier>.Instance,
            new HttpClient(),
            ageRatingService: ageRatingService);
}
