using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Events;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public sealed class WatchedStatusServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly WatchedStatusService _service;
    private readonly ApplicationUser _userA = new() { Id = "user-a", UserName = "user-a" };
    private readonly ApplicationUser _userB = new() { Id = "user-b", UserName = "user-b" };
    private Movie _movie = null!;
    private TVShowEpisode _episode = null!;

    public WatchedStatusServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new ApplicationDbContext(options, new EventManager());
        _db.Database.EnsureCreated();
        SeedAsync().GetAwaiter().GetResult();
        _service = new WatchedStatusService(_db);
    }

    [Fact]
    public async Task MarkWatchedAsync_StoresMovieAndEpisodePerUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieWatchedAt = new DateTime(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc);
        var episodeWatchedAt = new DateTime(2026, 8, 30, 10, 5, 0, DateTimeKind.Utc);

        await _service.MarkWatchedAsync(_userA.Id, _movie.Id, null, movieWatchedAt, ct);
        await _service.MarkWatchedAsync(_userA.Id, null, _episode.Id, episodeWatchedAt, ct);

        var result = await _service.GetWatchedAtAsync(_userA.Id, [_movie.Id], [_episode.Id], ct);

        Assert.Equal(movieWatchedAt, result.MovieWatchedAt[_movie.Id]);
        Assert.Equal(episodeWatchedAt, result.EpisodeWatchedAt[_episode.Id]);
    }

    [Fact]
    public async Task GetWatchedAtAsync_IsUserScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        await _service.MarkWatchedAsync(_userB.Id, _movie.Id, null, DateTime.UtcNow, ct);

        var result = await _service.GetWatchedAtAsync(_userA.Id, [_movie.Id], [], ct);

        Assert.Empty(result.MovieWatchedAt);
    }

    [Fact]
    public async Task MarkWatchedAsync_UpdatesExistingEntryWithoutDuplicate()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = new DateTime(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc);
        var second = first.AddMinutes(2);

        await _service.MarkWatchedAsync(_userA.Id, _movie.Id, null, first, ct);
        await _service.MarkWatchedAsync(_userA.Id, _movie.Id, null, second, ct);

        var entries = await _db.WatchedEntries.Where(x => x.UserId == _userA.Id && x.MovieId == _movie.Id).ToListAsync(ct);
        Assert.Single(entries);
        Assert.Equal(second, entries[0].WatchedAt);
    }

    [Fact]
    public async Task Database_RejectsBothOrNoTitleReferences()
    {
        var ct = TestContext.Current.CancellationToken;

        _db.WatchedEntries.Add(new WatchedEntry
        {
            UserId = _userA.Id,
            MovieId = _movie.Id,
            TVShowEpisodeId = _episode.Id,
            WatchedAt = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync(ct));
        _db.ChangeTracker.Clear();

        _db.WatchedEntries.Add(new WatchedEntry
        {
            UserId = _userA.Id,
            WatchedAt = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Database_RejectsDuplicateMovieForSameUser()
    {
        var ct = TestContext.Current.CancellationToken;
        _db.WatchedEntries.AddRange(
            new WatchedEntry { UserId = _userA.Id, MovieId = _movie.Id, WatchedAt = DateTime.UtcNow },
            new WatchedEntry { UserId = _userA.Id, MovieId = _movie.Id, WatchedAt = DateTime.UtcNow.AddMinutes(1) });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task EnrichAsync_SetsOnlyMovieAndEpisodeWatchedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var watchedAt = new DateTime(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc);
        await _service.MarkWatchedAsync(_userA.Id, _movie.Id, null, watchedAt, ct);

        var movie = new DtoMovie { Id = _movie.Id };
        var episode = new DtoTVShowEpisode { Id = _episode.Id };
        var collection = new DtoMovieCollection { Id = 900 };

        await _service.EnrichAsync(_userA.Id, [movie, episode, collection], ct);

        Assert.Equal(watchedAt, movie.WatchedAt);
        Assert.Null(episode.WatchedAt);
        Assert.Null(collection.WatchedAt);
    }

    private async Task SeedAsync()
    {
        var source = new MediaSource { Name = "Source", Path = "/media", Host = "localhost", Port = 22, CreatedAt = DateTime.UtcNow };
        _db.MediaSources.Add(source);
        _db.Users.AddRange(_userA, _userB);
        await _db.SaveChangesAsync();

        _movie = new Movie { Name = "Movie", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(_movie);
        var show = new TVShow { Name = "Show", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        _db.TVShows.Add(show);
        await _db.SaveChangesAsync();

        var season = new TVShowSeason { Name = "Season 01", TVShowId = show.Id, MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        _db.TVShowSeasons.Add(season);
        await _db.SaveChangesAsync();

        _episode = new TVShowEpisode { Name = "Episode", Number = 1, TVShowSeasonId = season.Id, MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        _db.TVShowEpisodes.Add(_episode);
        await _db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
