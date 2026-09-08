using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for the media search/listing endpoint <see cref="ItemsController.Get(long?, int, int, string?, long?, bool)"/>:
/// name search across all five media types (Movies, TVShows, TVShowSeasons, TVShowEpisodes,
/// MovieCollections), the "MovieCollection" type-field bugfix and hierarchy-based access control for the
/// three newly supported types (Movies via MovieCollectionId, TVShowSeasons/TVShowEpisodes via TVShowId).
/// </summary>
public class ItemsControllerTests_Search
{
    [Fact]
    public async Task Get_SearchMovies_ReturnsMatchingMovies()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        var matching = await CreateMovieAsync(db, source, "Breaking Point");
        await CreateMovieAsync(db, source, "Something Else");

        var actionResult = await controller.Get(null, 0, 30, "Breaking", null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var entry = Assert.Single(entries);
        Assert.Equal(nameof(Movie), entry.Type);
        Assert.Equal(matching.Id, entry.Id);
        Assert.Equal("Breaking Point", entry.Title);
    }

    [Fact]
    public async Task Get_SearchTVShowSeasons_ReturnsMatchingSeasons()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        var show = await CreateTVShowAsync(db, source, "Show");
        var matching = await CreateSeasonAsync(db, source, show, "Staffel Eins");
        await CreateSeasonAsync(db, source, show, "Andere Staffel");

        var actionResult = await controller.Get(null, 0, 30, "Staffel Eins", null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var entry = Assert.Single(entries);
        Assert.Equal(nameof(TVShowSeason), entry.Type);
        Assert.Equal(matching.Id, entry.Id);
    }

    [Fact]
    public async Task Get_SearchTVShowEpisodes_ReturnsMatchingEpisodes()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        var show = await CreateTVShowAsync(db, source, "Show");
        var season = await CreateSeasonAsync(db, source, show, "Staffel 1");
        var matching = await CreateEpisodeAsync(db, source, season, "Pilotfolge");
        await CreateEpisodeAsync(db, source, season, "Andere Folge");

        var actionResult = await controller.Get(null, 0, 30, "Pilotfolge", null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var entry = Assert.Single(entries);
        Assert.Equal(nameof(TVShowEpisode), entry.Type);
        Assert.Equal(matching.Id, entry.Id);
    }

    [Fact]
    public async Task Get_TypeFieldCorrect_MovieCollectionType()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        var collection = new MovieCollection { Name = "Meine Sammlung", MediaSourceId = source.Id };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var actionResult = await controller.Get(null, 0, 30, "Meine Sammlung");

        var entries = GetOkValue(actionResult);
        var entry = Assert.Single(entries);
        Assert.Equal(nameof(MovieCollection), entry.Type);
        Assert.NotEqual(nameof(Movie), entry.Type);
    }

    [Fact]
    public async Task Get_Movie_UserHasNoAccess_NotInResults()
    {
        var (db, controller, _, source) = await CreateControllerAsync();
        await CreateMovieAsync(db, source, "Gesperrter Film");

        var actionResult = await controller.Get(null, 0, 30, "Gesperrter Film", null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task Get_Movie_UserHasUnlockedMovieCollection_IncludedInResults()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = source.Id };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var movie = await CreateMovieInCollectionAsync(db, source, collection, "Freigeschalteter Film");
        db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = user.Id, MovieCollectionId = collection.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var actionResult = await controller.Get(null, 0, 30, "Freigeschalteter Film", null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var entry = Assert.Single(entries);
        Assert.Equal(movie.Id, entry.Id);
        Assert.Equal(nameof(Movie), entry.Type);
    }

    [Fact]
    public async Task Get_TVShowSeasonAndEpisode_UserHasUnlockedTVShow_IncludedInResults()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        var show = await CreateTVShowAsync(db, source, "Freigeschaltete Serie");
        var season = await CreateSeasonAsync(db, source, show, "Freigeschaltete Staffel");
        var episode = await CreateEpisodeAsync(db, source, season, "Freigeschaltete Episode");
        db.UnlockedMediaEntries.Add(new UnlockedMediaEntry { UserId = user.Id, TVShowId = show.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var seasonResult = GetOkValue(await controller.Get(null, 0, 30, "Freigeschaltete Staffel", null, includeIndividualMediaTypes: true));
        var episodeResult = GetOkValue(await controller.Get(null, 0, 30, "Freigeschaltete Episode", null, includeIndividualMediaTypes: true));

        var seasonEntry = Assert.Single(seasonResult);
        Assert.Equal(season.Id, seasonEntry.Id);
        Assert.Equal(nameof(TVShowSeason), seasonEntry.Type);

        var episodeEntry = Assert.Single(episodeResult);
        Assert.Equal(episode.Id, episodeEntry.Id);
        Assert.Equal(nameof(TVShowEpisode), episodeEntry.Type);
    }

    [Fact]
    public async Task Get_AllTypes_UserNoAccess_ReturnEmptyList()
    {
        var (db, controller, _, source) = await CreateControllerAsync();
        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = source.Id };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await CreateMovieInCollectionAsync(db, source, collection, "Film");
        var show = await CreateTVShowAsync(db, source, "Serie");
        var season = await CreateSeasonAsync(db, source, show, "Staffel");
        await CreateEpisodeAsync(db, source, season, "Episode");

        var actionResult = await controller.Get(null, 0, 30, null, null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task Get_SearchMovies_CaseInsensitive_LowerCase()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        var matching = await CreateMovieAsync(db, source, "Breaking Bad");

        var actionResult = await controller.Get(null, 0, 30, "breaking bad", null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var entry = Assert.Single(entries);
        Assert.Equal(matching.Id, entry.Id);
    }

    [Fact]
    public async Task Get_SearchMovies_CaseInsensitive_UpperCase()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        var matching = await CreateMovieAsync(db, source, "Breaking Bad");

        var actionResult = await controller.Get(null, 0, 30, "BREAKING", null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var entry = Assert.Single(entries);
        Assert.Equal(matching.Id, entry.Id);
    }

    [Fact]
    public async Task Get_SearchTVShowSeasons_CaseInsensitive()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        var show = await CreateTVShowAsync(db, source, "Show");
        var matching = await CreateSeasonAsync(db, source, show, "Staffel Eins");

        var actionResult = await controller.Get(null, 0, 30, "staffel", null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var entry = Assert.Single(entries);
        Assert.Equal(matching.Id, entry.Id);
    }

    [Fact]
    public async Task Get_SearchTVShowEpisodes_CaseInsensitive()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        var show = await CreateTVShowAsync(db, source, "Show");
        var season = await CreateSeasonAsync(db, source, show, "Staffel 1");
        var matching = await CreateEpisodeAsync(db, source, season, "Pilotfolge");

        var actionResult = await controller.Get(null, 0, 30, "pilotfolge", null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var entry = Assert.Single(entries);
        Assert.Equal(matching.Id, entry.Id);
    }

    [Fact]
    public async Task Get_WithMediaSourceId_IncludeIndividualMediaTypes_False_Returns2Types()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        await SeedAllFiveMediaTypesAsync(db, source);

        var actionResult = await controller.Get(source.Id, 0, 30, null, null, includeIndividualMediaTypes: false);

        var entries = GetOkValue(actionResult);
        Assert.All(entries, e => Assert.True(e.Type is nameof(MovieCollection) or nameof(TVShow)));
        Assert.Contains(entries, e => e.Type == nameof(MovieCollection));
        Assert.Contains(entries, e => e.Type == nameof(TVShow));
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task Get_WithMediaSourceId_IncludeIndividualMediaTypes_True_Returns5Types()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        await SeedAllFiveMediaTypesAsync(db, source);

        var actionResult = await controller.Get(source.Id, 0, 30, null, null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var types = entries.Select(e => e.Type).ToHashSet();
        Assert.Equal(
            new HashSet<string> { nameof(MovieCollection), nameof(TVShow), nameof(Movie), nameof(TVShowSeason), nameof(TVShowEpisode) },
            types);
    }

    [Fact]
    public async Task Get_PlaylistSearch_WithOptIn_Returns5Types()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        await SeedAllFiveMediaTypesAsync(db, source);

        var actionResult = await controller.Get(null, 0, 30, null, null, includeIndividualMediaTypes: true);

        var entries = GetOkValue(actionResult);
        var types = entries.Select(e => e.Type).ToHashSet();
        Assert.Equal(
            new HashSet<string> { nameof(MovieCollection), nameof(TVShow), nameof(Movie), nameof(TVShowSeason), nameof(TVShowEpisode) },
            types);
    }

    [Fact]
    public async Task Get_PlaylistSearch_WithoutOptIn_Returns2Types()
    {
        var (db, controller, user, source) = await CreateControllerAsync();
        await GrantSourceAccessAsync(db, user, source);
        await SeedAllFiveMediaTypesAsync(db, source);

        var actionResult = await controller.Get(null, 0, 30, null, null, includeIndividualMediaTypes: false);

        var entries = GetOkValue(actionResult);
        Assert.All(entries, e => Assert.True(e.Type is nameof(MovieCollection) or nameof(TVShow)));
        Assert.Contains(entries, e => e.Type == nameof(MovieCollection));
        Assert.Contains(entries, e => e.Type == nameof(TVShow));
        Assert.Equal(2, entries.Count);
    }

    private static List<MediaEntryDto> GetOkValue(ActionResult<List<MediaEntryDto>> actionResult)
    {
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        return Assert.IsType<List<MediaEntryDto>>(okResult.Value);
    }

    private static async Task GrantSourceAccessAsync(ApplicationDbContext db, ApplicationUser user, MediaSource source)
    {
        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = user.Id, MediaSourceId = source.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Movie> CreateMovieAsync(ApplicationDbContext db, MediaSource source, string name)
    {
        var movie = new Movie { Name = name, MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return movie;
    }

    private static async Task<Movie> CreateMovieInCollectionAsync(ApplicationDbContext db, MediaSource source, MovieCollection collection, string name)
    {
        var movie = new Movie { Name = name, MediaSourceId = source.Id, MovieCollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return movie;
    }

    private static async Task<TVShow> CreateTVShowAsync(ApplicationDbContext db, MediaSource source, string name)
    {
        var show = new TVShow { Name = name, MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.TVShows.Add(show);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return show;
    }

    private static async Task<TVShowSeason> CreateSeasonAsync(ApplicationDbContext db, MediaSource source, TVShow show, string name)
    {
        var season = new TVShowSeason { Name = name, TVShowId = show.Id, MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.TVShowSeasons.Add(season);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return season;
    }

    private static async Task<TVShowEpisode> CreateEpisodeAsync(ApplicationDbContext db, MediaSource source, TVShowSeason season, string name)
    {
        var episode = new TVShowEpisode { Name = name, Number = 1, TVShowSeasonId = season.Id, MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.TVShowEpisodes.Add(episode);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return episode;
    }

    private static async Task SeedAllFiveMediaTypesAsync(ApplicationDbContext db, MediaSource source)
    {
        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = source.Id };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await CreateMovieAsync(db, source, "Einzelfilm");
        var show = await CreateTVShowAsync(db, source, "Serie");
        var season = await CreateSeasonAsync(db, source, show, "Staffel");
        await CreateEpisodeAsync(db, source, season, "Episode");
    }

    private static async Task<(ApplicationDbContext db, ItemsController controller, ApplicationUser user, MediaSource source)> CreateControllerAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = $"Data Source=file:items-search-tests-{Guid.NewGuid()}?mode=memory&cache=shared";
        var (db, controller, user) = await ItemsControllerTestFactory.CreateAsync(connectionString, "search-user@test.com", cancellationToken: ct);

        var source = new MediaSource { Name = "Search Source", Path = "/s", Host = "localhost", Port = 22 };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        return (db, controller, user, source);
    }
}
