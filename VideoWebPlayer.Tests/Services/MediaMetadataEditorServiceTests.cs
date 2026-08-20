using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class MediaMetadataEditorServiceTests
{
    [Fact]
    public async Task UpdateMovieAsync_SavesMetadataAndCreatesNewGenres()
    {
        await using var db = CreateDb();
        db.Movies.Add(new Movie
        {
            Id = 10,
            MediaSourceId = 1,
            Name = "Old Movie",
            CreatedAt = DateTime.UtcNow,
            GenreNames = "Action",
            MovieGenres =
            {
                new MovieGenre { MovieId = 10, GenreId = 100 },
            },
        });
        db.Genres.Add(new Genre
        {
            Id = 100,
            MediaSourceId = 1,
            Name = "Action",
        });
        await db.SaveChangesAsync();

        var service = new MediaMetadataEditorService(db);
        await service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = "movie",
            Id = 10,
            Name = "New Movie",
            ReleaseDate = new DateTime(2024, 5, 6, 11, 30, 0),
            Plot = " New plot ",
            GenreNames = ["Action", "Noir", "noir"],
        });

        var movie = await db.Movies
            .Include(m => m.MovieGenres)
            .SingleAsync(m => m.Id == 10);
        Assert.Equal("New Movie", movie.Name);
        Assert.Equal(new DateTime(2024, 5, 6), movie.ReleaseDate);
        Assert.Equal("New plot", movie.Plot);
        Assert.True(movie.IsManuallyEdited);
        Assert.Equal("Action,Noir", movie.GenreNames);
        Assert.Equal(2, movie.MovieGenres.Count);
        Assert.Contains(await db.Genres.ToListAsync(), g => g.Name == "Noir" && g.MediaSourceId == 1);
    }

    [Fact]
    public async Task GetGenreOptionsAsync_ReturnsDistinctSortedGenreNames()
    {
        await using var db = CreateDb();
        db.Genres.AddRange(
            new Genre { Id = 1, MediaSourceId = 1, Name = "Drama" },
            new Genre { Id = 2, MediaSourceId = 1, Name = "action" },
            new Genre { Id = 3, MediaSourceId = 1, Name = "Action" },
            new Genre { Id = 4, MediaSourceId = 1, Name = "  Comedy  " });
        await db.SaveChangesAsync();

        var options = await new MediaMetadataEditorService(db).GetGenreOptionsAsync();

        Assert.Collection(
            options,
            option => Assert.Equal("Comedy", option.Name),
            option => Assert.Equal("action", option.Name),
            option => Assert.Equal("Drama", option.Name));
    }

    [Fact]
    public async Task UpdateTVShowSeasonAsync_RejectsPlotAndGenres()
    {
        await using var db = CreateDb();
        db.TVShowSeasons.Add(new TVShowSeason
        {
            Id = 20,
            TVShowId = 7,
            MediaSourceId = 1,
            Name = "Season 1",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new MediaMetadataEditorService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = "tvshowseason",
            Id = 20,
            Name = "Season 1",
            PremieredAt = new DateTime(2024, 1, 1),
            Plot = "Should not be accepted",
            GenreNames = ["Drama"],
        }));
    }

    [Theory]
    [InlineData("movie")]
    [InlineData("moviecollection")]
    [InlineData("tvshow")]
    public async Task UpdateAsync_RejectsPremieredAtForReleaseDateTypes(string objectType)
    {
        await using var db = CreateDb();
        var service = new MediaMetadataEditorService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = objectType,
            Id = 1,
            Name = "Title",
            ReleaseDate = new DateTime(2024, 1, 1),
            PremieredAt = new DateTime(2024, 1, 2),
        }));

        Assert.Contains("PremieredAt", ex.Message);
    }

    [Theory]
    [InlineData("tvshowseason")]
    [InlineData("tvshowepisode")]
    public async Task UpdateAsync_RejectsReleaseDateForPremieredAtTypes(string objectType)
    {
        await using var db = CreateDb();
        var service = new MediaMetadataEditorService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = objectType,
            Id = 1,
            Name = "Title",
            ReleaseDate = new DateTime(2024, 1, 1),
            PremieredAt = new DateTime(2024, 1, 2),
        }));

        Assert.Contains("ReleaseDate", ex.Message);
    }

    [Fact]
    public async Task UpdateTVShowAsync_SavesGenresAndRejectsPremieredAt()
    {
        await using var db = CreateDb();
        db.TVShows.Add(new TVShow
        {
            Id = 40,
            MediaSourceId = 1,
            Name = "Old Show",
            CreatedAt = DateTime.UtcNow,
        });
        db.Genres.Add(new Genre
        {
            Id = 101,
            MediaSourceId = 1,
            Name = "Drama",
        });
        await db.SaveChangesAsync();

        var service = new MediaMetadataEditorService(db);
        await service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = "tvshow",
            Id = 40,
            Name = "New Show",
            ReleaseDate = new DateTime(2024, 6, 7, 8, 9, 0),
            Plot = " Show plot ",
            GenreNames = ["Drama", "Mystery"],
        });

        var show = await db.TVShows
            .Include(s => s.TVShowGenres)
            .SingleAsync(s => s.Id == 40);
        Assert.Equal("New Show", show.Name);
        Assert.Equal(new DateTime(2024, 6, 7), show.ReleaseDate);
        Assert.Equal("Show plot", show.Plot);
        Assert.True(show.IsManuallyEdited);
        Assert.Equal("Drama,Mystery", show.GenreNames);
        Assert.Equal(2, show.TVShowGenres.Count);
    }

    [Fact]
    public async Task UpdateTVShowEpisodeAsync_SavesExistingEpisodeFieldsOnly()
    {
        await using var db = CreateDb();
        db.TVShowEpisodes.Add(new TVShowEpisode
        {
            Id = 30,
            TVShowSeasonId = 20,
            MediaSourceId = 1,
            Name = "Old Episode",
            CreatedAt = DateTime.UtcNow,
            Plot = "Old plot",
        });
        await db.SaveChangesAsync();

        var service = new MediaMetadataEditorService(db);
        await service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = "tvshowepisode",
            Id = 30,
            Name = "New Episode",
            PremieredAt = new DateTime(2024, 8, 9, 13, 45, 0),
            Plot = " New episode plot ",
        });

        var episode = await db.TVShowEpisodes.SingleAsync(e => e.Id == 30);
        Assert.Equal("New Episode", episode.Name);
        Assert.Equal(new DateTime(2024, 8, 9), episode.PremieredAt);
        Assert.Equal("New episode plot", episode.Plot);
        Assert.True(episode.IsManuallyEdited);
    }

    [Fact]
    public async Task UpdateMovieCollectionAsync_SavesReleaseDateAndRejectsGenres()
    {
        await using var db = CreateDb();
        db.MovieCollections.Add(new MovieCollection
        {
            Id = 50,
            MediaSourceId = 1,
            Name = "Old Collection",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new MediaMetadataEditorService(db);
        await service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = "moviecollection",
            Id = 50,
            Name = "New Collection",
            ReleaseDate = new DateTime(2024, 10, 11, 12, 13, 0),
        });

        var collection = await db.MovieCollections.SingleAsync(c => c.Id == 50);
        Assert.Equal("New Collection", collection.Name);
        Assert.Equal(new DateTime(2024, 10, 11), collection.ReleaseDate);
        Assert.True(collection.IsManuallyEdited);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = "moviecollection",
            Id = 50,
            Name = "New Collection",
            GenreNames = ["Action"],
        }));
    }

    [Theory]
    [InlineData("movie", 0, "Title")]
    [InlineData("movie", -1, "Title")]
    [InlineData("movie", 1, "")]
    public async Task UpdateAsync_RejectsInvalidIdsAndNames(string objectType, long id, string name)
    {
        await using var db = CreateDb();
        var service = new MediaMetadataEditorService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = objectType,
            Id = id,
            Name = name,
        }));
    }

    [Fact]
    public async Task UpdateAsync_RejectsTooLongName()
    {
        await using var db = CreateDb();
        var service = new MediaMetadataEditorService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new MediaMetadataUpdateRequest
        {
            ObjectType = "movie",
            Id = 1,
            Name = new string('x', 513),
        }));

        Assert.Contains("512", ex.Message);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"metadata-editor-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options, new EventManager());
    }
}
