using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Events;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

public class ApplicationDbContextTests
{
    [Fact]
    public async Task DeleteMediaSourceAsync_RemovesSourceAndAllRelatedEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:deletion-tests?mode=memory&cache=shared";
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        var source = new MediaSource
        {
            Name = "Test Source",
            Path = "/media",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var collection = new MediaCollection
        {
            Name = "Root",
            Path = "/media",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        var mediaItem = new MediaItem
        {
            Name = "movie.mp4",
            Path = "/media/movie.mp4",
            MediaCollection = collection,
            MediaCollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);

        var movieCollection = new MovieCollection
        {
            Name = "Action",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MovieCollections.Add(movieCollection);
        await db.SaveChangesAsync(ct);

        var movie = new Movie
        {
            Name = "Test Movie",
            MediaSourceId = source.Id,
            MovieCollection = movieCollection,
            MovieCollectionId = movieCollection.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);

        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = mediaItem.Id });
        await db.SaveChangesAsync(ct);

        var genre = new Genre { Name = "Action", MediaSourceId = source.Id };
        db.Genres.Add(genre);
        await db.SaveChangesAsync(ct);

        db.GenreNames.Add(new GenreName { Name = "Action-Alt", GenreId = genre.Id });
        db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genre.Id });
        await db.SaveChangesAsync(ct);

        var tvShow = new TVShow
        {
            Name = "Test Show",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShows.Add(tvShow);
        await db.SaveChangesAsync(ct);

        var season = new TVShowSeason
        {
            Name = "Season 1",
            TVShow = tvShow,
            TVShowId = tvShow.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShowSeasons.Add(season);
        await db.SaveChangesAsync(ct);

        var episode = new TVShowEpisode
        {
            Name = "Episode 1",
            Number = 1,
            TVShowSeason = season,
            TVShowSeasonId = season.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync(ct);

        var user = new ApplicationUser { Id = "user1", UserName = "user1" };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        db.TVShowEpisodeMediaItems.Add(new TVShowEpisodeMediaItem { TVShowEpisodeId = episode.Id, MediaItemId = mediaItem.Id });
        db.TVShowGenres.Add(new TVShowGenre { TVShowId = tvShow.Id, GenreId = genre.Id });
        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = user.Id });
        db.WatchedEntries.Add(new WatchedEntry { UserId = user.Id, MovieId = movie.Id, WatchedAt = DateTime.UtcNow });
        db.WatchedEntries.Add(new WatchedEntry { UserId = user.Id, TVShowEpisodeId = episode.Id, WatchedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);

        var progressValues = new List<double>();
        var progress = new ImmediateProgress(p => progressValues.Add(p));

        await db.DeleteMediaSourceAsync(source, progress, ct);

        Assert.Null(await db.MediaSources.FindAsync(new object[] { source.Id }, ct));
        Assert.Empty(await db.MediaCollections.Where(c => c.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.MediaItems.Where(i => i.MediaCollection.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.Movies.Where(m => m.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.MovieCollections.Where(mc => mc.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.TVShows.Where(t => t.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.TVShowSeasons.Where(s => s.TVShow.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.TVShowEpisodes.Where(e => e.TVShowSeason.TVShow.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.MovieMediaItems.ToListAsync(ct));
        Assert.Empty(await db.TVShowEpisodeMediaItems.ToListAsync(ct));
        Assert.Empty(await db.MovieGenres.Where(mg => mg.Movie.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.TVShowGenres.Where(tg => tg.TVShow.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.WatchedEntries.ToListAsync(ct));
        Assert.Empty(await db.GenreNames.Where(gn => gn.Genre.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.Genres.Where(g => g.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.MediaSourceUsers.Where(msu => msu.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.NotEmpty(progressValues);
        Assert.Equal(1.0, progressValues.Last(), 3);
    }

    private sealed class DeleteTestContext : IDisposable
    {
        public SqliteConnection Connection { get; set; } = null!;
        public IServiceScope Scope { get; set; } = null!;
        public ApplicationDbContext Db { get; set; } = null!;
        public EventManager EventManager { get; set; } = null!;
        public MediaSource Source { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public void Dispose()
        {
            Scope?.Dispose();
            Connection?.Dispose();
        }
    }

    private async Task<DeleteTestContext> SeedFullSourceAsync(string dbName, CancellationToken ct)
    {
        var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        var serviceProvider = services.BuildServiceProvider();

        var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var eventManager = scope.ServiceProvider.GetRequiredService<EventManager>();
        await db.Database.EnsureCreatedAsync(ct);

        var user = new ApplicationUser { Id = "user1", UserName = "user1" };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var source = new MediaSource
        {
            Name = "Test Source",
            Path = "/media",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        var collection = new MediaCollection
        {
            Name = "Root",
            Path = "/media",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        var mediaItem = new MediaItem
        {
            Name = "movie.mp4",
            Path = "/media/movie.mp4",
            MediaCollection = collection,
            MediaCollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);

        var movieCollection = new MovieCollection
        {
            Name = "Action",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MovieCollections.Add(movieCollection);
        await db.SaveChangesAsync(ct);

        var movie = new Movie
        {
            Name = "Test Movie",
            MediaSourceId = source.Id,
            MovieCollection = movieCollection,
            MovieCollectionId = movieCollection.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);

        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = mediaItem.Id });
        await db.SaveChangesAsync(ct);

        var genre = new Genre { Name = "Action", MediaSourceId = source.Id };
        db.Genres.Add(genre);
        await db.SaveChangesAsync(ct);

        db.GenreNames.Add(new GenreName { Name = "Action-Alt", GenreId = genre.Id });
        db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genre.Id });
        await db.SaveChangesAsync(ct);

        var tvShow = new TVShow
        {
            Name = "Test Show",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShows.Add(tvShow);
        await db.SaveChangesAsync(ct);

        var season = new TVShowSeason
        {
            Name = "Season 1",
            TVShow = tvShow,
            TVShowId = tvShow.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShowSeasons.Add(season);
        await db.SaveChangesAsync(ct);

        var episode = new TVShowEpisode
        {
            Name = "Episode 1",
            Number = 1,
            TVShowSeason = season,
            TVShowSeasonId = season.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync(ct);

        db.TVShowEpisodeMediaItems.Add(new TVShowEpisodeMediaItem { TVShowEpisodeId = episode.Id, MediaItemId = mediaItem.Id });
        db.TVShowGenres.Add(new TVShowGenre { TVShowId = tvShow.Id, GenreId = genre.Id });
        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = user.Id });
        db.WatchedEntries.Add(new WatchedEntry { UserId = user.Id, MovieId = movie.Id, WatchedAt = DateTime.UtcNow });
        db.WatchedEntries.Add(new WatchedEntry { UserId = user.Id, TVShowEpisodeId = episode.Id, WatchedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);

        db.Pictures.Add(new Picture
        {
            MediaItemId = mediaItem.Id,
            Type = "poster",
            Data = [0x01],
            ContentType = "image/png"
        });
        var backgroundPicture = new Picture
        {
            MediaItemId = mediaItem.Id,
            EpisodeId = episode.Id,
            Type = "thumb",
            Data = [0x02],
            ContentType = "image/png",
            IsGeneratedBackground = true
        };
        db.Pictures.Add(backgroundPicture);
        await db.SaveChangesAsync(ct);

        episode.GeneratedBackgroundPictureId = backgroundPicture.Id;
        db.TVShowEpisodes.Update(episode);
        await db.SaveChangesAsync(ct);

        db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = user.Id,
            MovieId = movie.Id,
            TVShowEpisodeId = episode.Id,
            Position = TimeSpan.Zero,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        db.FavoriteEntries.Add(new FavoriteEntry
        {
            UserId = user.Id,
            MovieId = movie.Id,
            TVShowEpisodeId = episode.Id,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        db.RecentEntries.Add(new RecentEntry
        {
            MediaSourceId = source.Id,
            MovieId = movie.Id,
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        db.RecentEntries.Add(new RecentEntry
        {
            MediaSourceId = source.Id,
            TVShowEpisodeId = episode.Id,
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return new DeleteTestContext
        {
            Connection = connection,
            Scope = scope,
            Db = db,
            EventManager = eventManager,
            Source = source,
            User = user
        };
    }

    private async Task<DeleteTestContext> SeedSourceOnlyAsync(string dbName, CancellationToken ct)
    {
        var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        var serviceProvider = services.BuildServiceProvider();

        var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var eventManager = scope.ServiceProvider.GetRequiredService<EventManager>();
        await db.Database.EnsureCreatedAsync(ct);

        var source = new MediaSource
        {
            Name = "Isolated Source",
            Path = "/other",
            Host = "otherhost",
            Port = 23,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        return new DeleteTestContext
        {
            Connection = connection,
            Scope = scope,
            Db = db,
            EventManager = eventManager,
            Source = source,
            User = null!
        };
    }

    [Fact]
    public async Task DeleteMediaSourceAsync_DoesNotAffectOtherSources()
    {
        var ct = TestContext.Current.CancellationToken;

        using var ctx1 = await SeedFullSourceAsync("source-iso-1", ct);
        using var ctx2 = await SeedFullSourceAsync("source-iso-2", ct);

        await ctx1.Db.DeleteMediaSourceAsync(ctx1.Source, null, ct);

        Assert.Null(await ctx1.Db.MediaSources.FindAsync(new object[] { ctx1.Source.Id }, ct));
        Assert.Empty(await ctx1.Db.Movies.ToListAsync(ct));
        Assert.Empty(await ctx1.Db.TVShows.ToListAsync(ct));
        Assert.Empty(await ctx1.Db.Genres.ToListAsync(ct));
        Assert.Empty(await ctx1.Db.MediaItems.ToListAsync(ct));

        Assert.NotNull(await ctx2.Db.MediaSources.FindAsync(new object[] { ctx2.Source.Id }, ct));
        Assert.Single(await ctx2.Db.Movies.ToListAsync(ct));
        Assert.Single(await ctx2.Db.TVShows.ToListAsync(ct));
        Assert.Single(await ctx2.Db.Genres.ToListAsync(ct));
        Assert.Single(await ctx2.Db.MediaItems.ToListAsync(ct));
        Assert.Single(await ctx2.Db.MediaCollections.ToListAsync(ct));
        Assert.Single(await ctx2.Db.MovieCollections.ToListAsync(ct));
        Assert.Single(await ctx2.Db.TVShowSeasons.ToListAsync(ct));
        Assert.Single(await ctx2.Db.TVShowEpisodes.ToListAsync(ct));
        Assert.Single(await ctx2.Db.MovieMediaItems.ToListAsync(ct));
        Assert.Single(await ctx2.Db.TVShowEpisodeMediaItems.ToListAsync(ct));
        Assert.Single(await ctx2.Db.GenreNames.ToListAsync(ct));
        Assert.Single(await ctx2.Db.MovieGenres.ToListAsync(ct));
        Assert.Single(await ctx2.Db.TVShowGenres.ToListAsync(ct));
        Assert.Equal(2, await ctx2.Db.WatchedEntries.CountAsync(ct));
        Assert.Single(await ctx2.Db.MediaSourceUsers.ToListAsync(ct));
    }

    [Fact]
    public async Task DeleteMediaSourceAsync_PublishesMediaSourceDeletedEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ctx = await SeedFullSourceAsync("source-event", ct);

        MediaSourceDeletedEvent? published = null;
        ctx.EventManager.Subscribe<MediaSourceDeletedEvent>(e => published = e);

        await ctx.Db.DeleteMediaSourceAsync(ctx.Source, null, ct);

        Assert.NotNull(published);
        Assert.Equal(ctx.Source.Id, published.Source.Id);
    }

    [Fact]
    public async Task DeleteMediaSourceAsync_ThrowsArgumentNullException()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ctx = await SeedSourceOnlyAsync("source-null", ct);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await ctx.Db.DeleteMediaSourceAsync(null!, null, ct));
    }

    [Fact]
    public async Task DeleteMediaSourceAsync_SkipsNonExistingSource()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ctx = await SeedSourceOnlyAsync("source-missing", ct);

        var progressValues = new List<double>();
        var missing = new MediaSource { Id = 999 };

        await ctx.Db.DeleteMediaSourceAsync(missing, new Progress<double>(p => progressValues.Add(p)), ct);

        Assert.Empty(progressValues);
        Assert.NotNull(await ctx.Db.MediaSources.FindAsync(new object[] { ctx.Source.Id }, ct));
    }

    [Fact]
    public async Task DeleteMediaSourceAsync_CanDeleteSourceWithoutRelatedEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ctx = await SeedSourceOnlyAsync("source-minimal", ct);

        var progressValues = new List<double>();
        await ctx.Db.DeleteMediaSourceAsync(ctx.Source, new ImmediateProgress(p => progressValues.Add(p)), ct);

        Assert.Null(await ctx.Db.MediaSources.FindAsync(new object[] { ctx.Source.Id }, ct));
        Assert.Equal(1.0, progressValues.Last(), 3);
    }

    [Fact]
    public async Task DeleteMediaSourceAsync_RemovesPicturesAndActivityEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ctx = await SeedFullSourceAsync("source-activity", ct);

        Assert.Equal(2, await ctx.Db.Pictures.CountAsync(ct));
        Assert.Single(await ctx.Db.ContinueWatchingEntries.ToListAsync(ct));
        Assert.Single(await ctx.Db.FavoriteEntries.ToListAsync(ct));
        Assert.Equal(2, await ctx.Db.WatchedEntries.CountAsync(ct));
        Assert.Equal(2, await ctx.Db.RecentEntries.CountAsync(ct));

        await ctx.Db.DeleteMediaSourceAsync(ctx.Source, null, ct);

        Assert.Null(await ctx.Db.MediaSources.FindAsync(new object[] { ctx.Source.Id }, ct));
        Assert.Empty(await ctx.Db.Pictures.ToListAsync(ct));
        Assert.Empty(await ctx.Db.ContinueWatchingEntries.ToListAsync(ct));
        Assert.Empty(await ctx.Db.FavoriteEntries.ToListAsync(ct));
        Assert.Empty(await ctx.Db.WatchedEntries.ToListAsync(ct));
        Assert.Empty(await ctx.Db.RecentEntries.ToListAsync(ct));
    }

    [Fact]
    public async Task DeleteMediaSourceAsync_RemovesPicturesAssignedToMediaBaseEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ctx = await SeedFullSourceAsync("source-pictures-on-entries", ct);

        var mediaItem = await ctx.Db.MediaItems.FirstAsync(ct);
        var movie = await ctx.Db.Movies.FirstAsync(ct);
        var tvShow = await ctx.Db.TVShows.FirstAsync(ct);
        var season = await ctx.Db.TVShowSeasons.FirstAsync(ct);
        var movieCollection = await ctx.Db.MovieCollections.FirstAsync(ct);
        var episode = await ctx.Db.TVShowEpisodes.FirstAsync(ct);

        var moviePoster = new Picture { MediaItemId = mediaItem.Id, Type = "poster", Data = [0x11], ContentType = "image/png" };
        var movieBanner = new Picture { MediaItemId = mediaItem.Id, Type = "banner", Data = [0x12], ContentType = "image/png" };
        var movieFanart = new Picture { MediaItemId = mediaItem.Id, Type = "fanart", Data = [0x13], ContentType = "image/png" };
        var tvShowPoster = new Picture { MediaItemId = mediaItem.Id, Type = "poster", Data = [0x21], ContentType = "image/png" };
        var seasonBanner = new Picture { MediaItemId = mediaItem.Id, Type = "banner", Data = [0x31], ContentType = "image/png" };
        var collectionFanart = new Picture { MediaItemId = mediaItem.Id, Type = "fanart", Data = [0x41], ContentType = "image/png" };
        var episodePoster = new Picture { MediaItemId = mediaItem.Id, Type = "poster", Data = [0x51], ContentType = "image/png" };

        ctx.Db.Pictures.AddRange(moviePoster, movieBanner, movieFanart, tvShowPoster, seasonBanner, collectionFanart, episodePoster);
        await ctx.Db.SaveChangesAsync(ct);

        movie.PosterPictureId = moviePoster.Id;
        movie.BannerPictureId = movieBanner.Id;
        movie.FanartPictureId = movieFanart.Id;
        tvShow.PosterPictureId = tvShowPoster.Id;
        season.BannerPictureId = seasonBanner.Id;
        movieCollection.FanartPictureId = collectionFanart.Id;
        episode.PosterPictureId = episodePoster.Id;
        await ctx.Db.SaveChangesAsync(ct);

        var pictureCountBefore = await ctx.Db.Pictures.CountAsync(ct);
        Assert.Equal(9, pictureCountBefore);

        await ctx.Db.DeleteMediaSourceAsync(ctx.Source, null, ct);

        Assert.Null(await ctx.Db.MediaSources.FindAsync(new object[] { ctx.Source.Id }, ct));
        Assert.Empty(await ctx.Db.Pictures.ToListAsync(ct));
        Assert.Empty(await ctx.Db.Movies.ToListAsync(ct));
        Assert.Empty(await ctx.Db.TVShows.ToListAsync(ct));
        Assert.Empty(await ctx.Db.TVShowSeasons.ToListAsync(ct));
        Assert.Empty(await ctx.Db.TVShowEpisodes.ToListAsync(ct));
        Assert.Empty(await ctx.Db.MovieCollections.ToListAsync(ct));
    }

    [Fact]
    public async Task DeleteMediaSourceAsync_RemovesNestedMediaCollections()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ctx = await SeedFullSourceAsync("source-nested-collections", ct);

        var parent = await ctx.Db.MediaCollections.FirstAsync(mc => mc.MediaSourceId == ctx.Source.Id, ct);
        var child = new MediaCollection
        {
            Name = "Child",
            Path = "/media/child",
            MediaSourceId = ctx.Source.Id,
            ParentMediaCollectionId = parent.Id,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Db.MediaCollections.Add(child);
        await ctx.Db.SaveChangesAsync(ct);

        var mediaItem = new MediaItem
        {
            Name = "child.mp4",
            Path = "/media/child/child.mp4",
            MediaCollection = child,
            MediaCollectionId = child.Id,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Db.MediaItems.Add(mediaItem);
        await ctx.Db.SaveChangesAsync(ct);

        Assert.Equal(2, await ctx.Db.MediaCollections.CountAsync(ct));
        Assert.Equal(2, await ctx.Db.MediaItems.CountAsync(ct));

        await ctx.Db.DeleteMediaSourceAsync(ctx.Source, null, ct);

        Assert.Null(await ctx.Db.MediaSources.FindAsync(new object[] { ctx.Source.Id }, ct));
        Assert.Empty(await ctx.Db.MediaItems.ToListAsync(ct));
    }

    private sealed class ImmediateProgress : IProgress<double>
    {
        private readonly Action<double> _handler;

        public ImmediateProgress(Action<double> handler)
        {
            _handler = handler;
        }

        public void Report(double value)
        {
            _handler(value);
        }
    }
}
