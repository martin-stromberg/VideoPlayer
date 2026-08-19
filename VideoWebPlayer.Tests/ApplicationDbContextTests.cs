using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
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
        await db.SaveChangesAsync(ct);

        var progressValues = new List<double>();
        var progress = new Progress<double>(p => progressValues.Add(p));

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
        Assert.Empty(await db.GenreNames.Where(gn => gn.Genre.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.Genres.Where(g => g.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.Empty(await db.MediaSourceUsers.Where(msu => msu.MediaSourceId == source.Id).ToListAsync(ct));
        Assert.NotEmpty(progressValues);
        Assert.Equal(1.0, progressValues.Last(), 3);
    }
}
