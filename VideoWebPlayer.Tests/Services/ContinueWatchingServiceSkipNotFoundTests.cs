using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="ContinueWatchingService.SkipResult.NotFound"/>: a manual skip of an entry that does not
/// exist for the requesting user must report "not found" and must not touch anybody's data.
/// </summary>
public class ContinueWatchingServiceSkipNotFoundTests : ContinueWatchingServiceTestBase
{
    [Fact]
    public async Task SkipAsync_NoSuchEntry_ReturnsNotFound()
    {
        var result = await _service.SkipAsync(_testUserId, 999, null, ct: TestContext.Current.CancellationToken);

        Assert.Equal(ContinueWatchingService.SkipResult.NotFound, result);
    }

    [Fact]
    public async Task SkipAsync_BlankUserId_ReturnsNotFound()
    {
        var result = await _service.SkipAsync(" ", 1, null, ct: TestContext.Current.CancellationToken);

        Assert.Equal(ContinueWatchingService.SkipResult.NotFound, result);
    }

    /// <summary>
    /// The entry exists, but for another user: the caller gets "not found" and the other user's entry stays.
    /// </summary>
    [Fact]
    public async Task SkipAsync_EntryOfAnotherUser_ReturnsNotFound_AndKeepsThatEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = new Movie { Name = "Film", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync(ct);
        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = "other-user",
            MovieId = movie.Id,
            Position = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        });
        await _db.SaveChangesAsync(ct);

        var result = await _service.SkipAsync(_testUserId, movie.Id, null, ct: ct);

        Assert.Equal(ContinueWatchingService.SkipResult.NotFound, result);
        Assert.Single(_db.ContinueWatchingEntries.Where(e => e.UserId == "other-user" && e.MovieId == movie.Id));
    }
}
