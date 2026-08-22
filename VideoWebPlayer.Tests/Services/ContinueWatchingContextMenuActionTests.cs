using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests manual continue-watching context menu actions.
/// </summary>
public class ContinueWatchingContextMenuActionTests : ContinueWatchingServiceTestBase
{
    [Fact]
    public async Task HideAsync_RemovesOnlyEntryForCurrentUser()
    {
        var movie = await CreateMovieAsync("Movie");
        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movie.Id,
            Position = TimeSpan.FromMinutes(10),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 20
        });
        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = "other-user",
            MovieId = movie.Id,
            Position = TimeSpan.FromMinutes(12),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 10
        });
        await _db.SaveChangesAsync();

        var removed = await _service.HideAsync(_testUserId, movie.Id, null);

        Assert.True(removed);
        Assert.False(await _db.ContinueWatchingEntries.AnyAsync(e => e.UserId == _testUserId && e.MovieId == movie.Id));
        Assert.True(await _db.ContinueWatchingEntries.AnyAsync(e => e.UserId == "other-user" && e.MovieId == movie.Id));
    }

    [Fact]
    public async Task SkipAsync_Episode_ReplacesWithNextEpisodeAndKeepsListOrder()
    {
        await TestHelpers.CreateTvShowWithSeasonsAsync(
            _db,
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null) }));
        var episode1 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 1);
        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2);

        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            TVShowEpisodeId = episode1.Id,
            Position = TimeSpan.FromMinutes(18),
            Duration = TimeSpan.FromMinutes(45),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            ListOrder = 12345
        });
        await _db.SaveChangesAsync();

        var result = await _service.SkipAsync(_testUserId, null, episode1.Id);

        Assert.Equal(ContinueWatchingService.SkipResult.Replaced, result);
        var entry = await _db.ContinueWatchingEntries.SingleAsync(e => e.UserId == _testUserId);
        Assert.Equal(episode2.Id, entry.TVShowEpisodeId);
        Assert.Null(entry.MovieId);
        Assert.Equal(12345, entry.ListOrder);
        Assert.Equal(TimeSpan.Zero, entry.Position);
        Assert.Null(entry.Duration);
    }

    [Fact]
    public async Task SkipAsync_LastEpisode_RemovesEntryWithoutReplacement()
    {
        await TestHelpers.CreateTvShowWithSeasonsAsync(
            _db,
            ("Staffel 01", new (int, DateTime?)[] { (1, null) }));
        var episode = await _db.TVShowEpisodes.SingleAsync();

        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            TVShowEpisodeId = episode.Id,
            Position = TimeSpan.FromMinutes(18),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 12345
        });
        await _db.SaveChangesAsync();

        var result = await _service.SkipAsync(_testUserId, null, episode.Id);

        Assert.Equal(ContinueWatchingService.SkipResult.RemovedWithoutNext, result);
        Assert.False(await _db.ContinueWatchingEntries.AnyAsync(e => e.UserId == _testUserId));
    }

    [Fact]
    public async Task SkipAsync_Movie_ReplacesWithNextMovieAndKeepsListOrder()
    {
        var first = await CreateMovieAsync("A", new DateTime(2020, 1, 1));
        var second = await CreateMovieAsync("B", new DateTime(2021, 1, 1), first.MovieCollectionId);

        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = first.Id,
            Position = TimeSpan.FromMinutes(20),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
            ListOrder = 67890
        });
        await _db.SaveChangesAsync();

        var result = await _service.SkipAsync(_testUserId, first.Id, null);

        Assert.Equal(ContinueWatchingService.SkipResult.Replaced, result);
        var entry = await _db.ContinueWatchingEntries.SingleAsync(e => e.UserId == _testUserId);
        Assert.Equal(second.Id, entry.MovieId);
        Assert.Null(entry.TVShowEpisodeId);
        Assert.Equal(67890, entry.ListOrder);
        Assert.Equal(TimeSpan.Zero, entry.Position);
    }

    private async Task<Movie> CreateMovieAsync(string name, DateTime? releaseDate = null, long? collectionId = null)
    {
        if (collectionId is null)
        {
            var collection = new MovieCollection
            {
                Name = $"{name} Collection",
                MediaSourceId = 1,
                CreatedAt = DateTime.UtcNow
            };
            _db.MovieCollections.Add(collection);
            await _db.SaveChangesAsync();
            collectionId = collection.Id;
        }

        var movie = new Movie
        {
            Name = name,
            MovieCollectionId = collectionId,
            ReleaseDate = releaseDate,
            MediaSourceId = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie;
    }
}
