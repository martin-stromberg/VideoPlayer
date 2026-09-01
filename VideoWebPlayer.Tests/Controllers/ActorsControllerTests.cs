using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

public class ActorsControllerTests
{
    private static string CreateConnectionString() => $"Data Source=file:actors-{Guid.NewGuid()}?mode=memory&cache=shared";

    [Fact]
    public async Task GetActor_Aggregates_Movie_Collections_And_TV_Shows()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = CreateConnectionString();
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser { Id = userId, UserName = "regular@test.com" };
        var fakeAuth = new FakeAuthService { CurrentUser = user };

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IAuthService>(fakeAuth);
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        db.Users.Add(user);

        var source = new MediaSource { Name = "Test Source", Path = "/test", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = userId, MediaSourceId = source.Id });

        var allInCollection = new MovieCollection { Name = "All Movies", MediaSourceId = source.Id };
        var thresholdCollection = new MovieCollection { Name = "Threshold Movies", MediaSourceId = source.Id };
        var singleMovieCollection = new MovieCollection { Name = "Single Movie", MediaSourceId = source.Id };
        db.MovieCollections.AddRange(allInCollection, thresholdCollection, singleMovieCollection);

        var showAll = new TVShow { Name = "Show All", MediaSourceId = source.Id };
        var showPartial = new TVShow { Name = "Show Partial", MediaSourceId = source.Id };
        db.TVShows.AddRange(showAll, showPartial);
        await db.SaveChangesAsync(ct);

        var allSeason = new TVShowSeason { Name = "Staffel 1", TVShowId = showAll.Id };
        var partialSeasonA = new TVShowSeason { Name = "Staffel 1", TVShowId = showPartial.Id };
        var partialSeasonB = new TVShowSeason { Name = "Staffel 2", TVShowId = showPartial.Id };
        db.TVShowSeasons.AddRange(allSeason, partialSeasonA, partialSeasonB);
        await db.SaveChangesAsync(ct);

        var movies = new[]
        {
            new Movie { Name = "Alpha", MediaSourceId = source.Id, MovieCollectionId = allInCollection.Id },
            new Movie { Name = "Beta", MediaSourceId = source.Id, MovieCollectionId = allInCollection.Id },
            new Movie { Name = "Gamma", MediaSourceId = source.Id, MovieCollectionId = allInCollection.Id },
            new Movie { Name = "One", MediaSourceId = source.Id, MovieCollectionId = thresholdCollection.Id },
            new Movie { Name = "Two", MediaSourceId = source.Id, MovieCollectionId = thresholdCollection.Id },
            new Movie { Name = "Three", MediaSourceId = source.Id, MovieCollectionId = thresholdCollection.Id },
            new Movie { Name = "Standalone", MediaSourceId = source.Id, MovieCollectionId = singleMovieCollection.Id }
        };
        db.Movies.AddRange(movies);

        var episodes = new[]
        {
            new TVShowEpisode { Name = "A1", Number = 1, TVShowSeasonId = allSeason.Id },
            new TVShowEpisode { Name = "A2", Number = 2, TVShowSeasonId = allSeason.Id },
            new TVShowEpisode { Name = "P1", Number = 1, TVShowSeasonId = partialSeasonA.Id },
            new TVShowEpisode { Name = "P2", Number = 2, TVShowSeasonId = partialSeasonA.Id },
            new TVShowEpisode { Name = "P3", Number = 1, TVShowSeasonId = partialSeasonB.Id },
            new TVShowEpisode { Name = "P4", Number = 2, TVShowSeasonId = partialSeasonB.Id }
        };
        db.TVShowEpisodes.AddRange(episodes);

        var actor = new Actor { Name = "Test Actor", NormalizedName = "TEST ACTOR", CreatedAt = DateTime.UtcNow };
        db.Actors.Add(actor);
        await db.SaveChangesAsync(ct);

        db.MovieActors.AddRange(movies.Take(5).Select(m => new MovieActor { ActorId = actor.Id, MovieId = m.Id }));
        db.MovieActors.Add(new MovieActor { ActorId = actor.Id, MovieId = movies[6].Id });

        db.TVShowEpisodeActors.AddRange(episodes.Take(2).Select(e => new TVShowEpisodeActor { ActorId = actor.Id, TVShowEpisodeId = e.Id }));
        db.TVShowEpisodeActors.AddRange(episodes.Skip(2).Take(2).Select(e => new TVShowEpisodeActor { ActorId = actor.Id, TVShowEpisodeId = e.Id }));
        db.TVShowEpisodeActors.Add(new TVShowEpisodeActor { ActorId = actor.Id, TVShowEpisodeId = episodes[4].Id });
        await db.SaveChangesAsync(ct);

        var unlockedMediaService = scope.ServiceProvider.GetRequiredService<IUnlockedMediaService>();
        var controller = new ActorsController(fakeAuth, db, unlockedMediaService, NullLogger<ActorsController>.Instance);
        var result = await controller.GetActor(actor.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ActorDetailsDto>(okResult.Value);

        Assert.Equal(6, dto.Media.Count);

        var allEntry = Assert.Single(dto.Media, m => m.Type == "Filmsammlung" && m.Title == "All Movies");
        Assert.Equal("3 Filme", allEntry.Subtitle);

        var thresholdEntry = Assert.Single(dto.Media, m => m.Type == "Filmsammlung" && m.Title == "Threshold Movies");
        Assert.Contains("One", thresholdEntry.Subtitle);
        Assert.Contains("Two", thresholdEntry.Subtitle);

        Assert.Single(dto.Media, m => m.Type == "Film" && m.Title == "Standalone");

        var showAllEntry = Assert.Single(dto.Media, m => m.Type == "Serie" && m.Title == "Show All");
        Assert.Equal("2 Episoden", showAllEntry.Subtitle);

        var staffelEntry = Assert.Single(dto.Media, m => m.Type == "Staffel" && m.Title == "Show Partial - Staffel 1");
        Assert.Equal("2 Episoden", staffelEntry.Subtitle);

        var episodeEntry = Assert.Single(dto.Media, m => m.Type == "Episode" && m.Title == "P3");
        Assert.Equal("Show Partial - Staffel 2", episodeEntry.Subtitle);
    }

    [Fact]
    public async Task GetActors_Aggregated_Video_Counts_Match_Details()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = CreateConnectionString();
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser { Id = userId, UserName = "regular@test.com" };
        var fakeAuth = new FakeAuthService { CurrentUser = user };

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IAuthService>(fakeAuth);
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        db.Users.Add(user);

        var source = new MediaSource { Name = "Test Source", Path = "/test", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = userId, MediaSourceId = source.Id });

        var allInCollection = new MovieCollection { Name = "All Movies", MediaSourceId = source.Id };
        var thresholdCollection = new MovieCollection { Name = "Threshold Movies", MediaSourceId = source.Id };
        var singleMovieCollection = new MovieCollection { Name = "Single Movie", MediaSourceId = source.Id };
        db.MovieCollections.AddRange(allInCollection, thresholdCollection, singleMovieCollection);

        var showAll = new TVShow { Name = "Show All", MediaSourceId = source.Id };
        var showPartial = new TVShow { Name = "Show Partial", MediaSourceId = source.Id };
        db.TVShows.AddRange(showAll, showPartial);
        await db.SaveChangesAsync(ct);

        var allSeason = new TVShowSeason { Name = "Staffel 1", TVShowId = showAll.Id };
        var partialSeasonA = new TVShowSeason { Name = "Staffel 1", TVShowId = showPartial.Id };
        var partialSeasonB = new TVShowSeason { Name = "Staffel 2", TVShowId = showPartial.Id };
        db.TVShowSeasons.AddRange(allSeason, partialSeasonA, partialSeasonB);
        await db.SaveChangesAsync(ct);

        var movies = new[]
        {
            new Movie { Name = "Alpha", MediaSourceId = source.Id, MovieCollectionId = allInCollection.Id },
            new Movie { Name = "Beta", MediaSourceId = source.Id, MovieCollectionId = allInCollection.Id },
            new Movie { Name = "Gamma", MediaSourceId = source.Id, MovieCollectionId = allInCollection.Id },
            new Movie { Name = "One", MediaSourceId = source.Id, MovieCollectionId = thresholdCollection.Id },
            new Movie { Name = "Two", MediaSourceId = source.Id, MovieCollectionId = thresholdCollection.Id },
            new Movie { Name = "Three", MediaSourceId = source.Id, MovieCollectionId = thresholdCollection.Id },
            new Movie { Name = "Standalone", MediaSourceId = source.Id, MovieCollectionId = singleMovieCollection.Id }
        };
        db.Movies.AddRange(movies);

        var episodes = new[]
        {
            new TVShowEpisode { Name = "A1", Number = 1, TVShowSeasonId = allSeason.Id },
            new TVShowEpisode { Name = "A2", Number = 2, TVShowSeasonId = allSeason.Id },
            new TVShowEpisode { Name = "P1", Number = 1, TVShowSeasonId = partialSeasonA.Id },
            new TVShowEpisode { Name = "P2", Number = 2, TVShowSeasonId = partialSeasonA.Id },
            new TVShowEpisode { Name = "P3", Number = 1, TVShowSeasonId = partialSeasonB.Id },
            new TVShowEpisode { Name = "P4", Number = 2, TVShowSeasonId = partialSeasonB.Id }
        };
        db.TVShowEpisodes.AddRange(episodes);

        var actor = new Actor { Name = "Test Actor", NormalizedName = "TEST ACTOR", CreatedAt = DateTime.UtcNow };
        db.Actors.Add(actor);
        await db.SaveChangesAsync(ct);

        db.MovieActors.AddRange(movies.Take(5).Select(m => new MovieActor { ActorId = actor.Id, MovieId = m.Id }));
        db.MovieActors.Add(new MovieActor { ActorId = actor.Id, MovieId = movies[6].Id });

        db.TVShowEpisodeActors.AddRange(episodes.Take(2).Select(e => new TVShowEpisodeActor { ActorId = actor.Id, TVShowEpisodeId = e.Id }));
        db.TVShowEpisodeActors.AddRange(episodes.Skip(2).Take(2).Select(e => new TVShowEpisodeActor { ActorId = actor.Id, TVShowEpisodeId = e.Id }));
        db.TVShowEpisodeActors.Add(new TVShowEpisodeActor { ActorId = actor.Id, TVShowEpisodeId = episodes[4].Id });
        await db.SaveChangesAsync(ct);

        var unlockedMediaService = scope.ServiceProvider.GetRequiredService<IUnlockedMediaService>();
        var controller = new ActorsController(fakeAuth, db, unlockedMediaService, NullLogger<ActorsController>.Instance);
        var result = await controller.GetActors(null, "count");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var actors = Assert.IsAssignableFrom<IEnumerable<ActorDto>>(okResult.Value).ToList();

        Assert.Single(actors);
        Assert.Equal(6, actors[0].VideoCount);
    }

    [Fact]
    public async Task GetActors_Excludes_Actors_From_Inaccessible_Sources()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = CreateConnectionString();
        using var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser { Id = userId, UserName = "regular@test.com" };
        var fakeAuth = new FakeAuthService { CurrentUser = user };

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IAuthService>(fakeAuth);
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        db.Users.Add(user);

        var allowedSource = new MediaSource { Name = "Allowed", Path = "/a", Host = "localhost", Port = 22 };
        var otherSource = new MediaSource { Name = "Other", Path = "/b", Host = "localhost", Port = 22 };
        db.MediaSources.AddRange(allowedSource, otherSource);
        await db.SaveChangesAsync(ct);

        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = userId, MediaSourceId = allowedSource.Id });

        var allowedMovie = new Movie { Name = "Allowed Movie", MediaSourceId = allowedSource.Id };
        var otherMovie = new Movie { Name = "Other Movie", MediaSourceId = otherSource.Id };
        db.Movies.AddRange(allowedMovie, otherMovie);

        var allowedActor = new Actor { Name = "Allowed Actor", NormalizedName = "ALLOWED ACTOR", CreatedAt = DateTime.UtcNow };
        var otherActor = new Actor { Name = "Other Actor", NormalizedName = "OTHER ACTOR", CreatedAt = DateTime.UtcNow };
        db.Actors.AddRange(allowedActor, otherActor);
        await db.SaveChangesAsync(ct);

        db.MovieActors.AddRange(
            new MovieActor { ActorId = allowedActor.Id, MovieId = allowedMovie.Id },
            new MovieActor { ActorId = otherActor.Id, MovieId = otherMovie.Id });
        await db.SaveChangesAsync(ct);

        var unlockedMediaService = scope.ServiceProvider.GetRequiredService<IUnlockedMediaService>();
        var controller = new ActorsController(fakeAuth, db, unlockedMediaService, NullLogger<ActorsController>.Instance);
        var result = await controller.GetActors(null, null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var actors = Assert.IsAssignableFrom<IEnumerable<ActorDto>>(okResult.Value);
        var names = actors.Select(a => a.Name).ToList();

        Assert.Contains("Allowed Actor", names);
        Assert.DoesNotContain("Other Actor", names);
    }

    private sealed class FakeAuthService : IAuthService
    {
        public ApplicationUser? CurrentUser { get; set; }

        public Task<AuthorizationToken> ImpersonateAsync(ImpersonateRequest request)
        {
            return Task.FromResult(new AuthorizationToken());
        }

        public Task<AuthorizationToken> LoginAsync(AuthenticationRequest request)
        {
            return Task.FromResult(new AuthorizationToken());
        }
    }
}
