using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VideoWebPlayer.Data;
using VideoWebPlayer.Events;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class SeasonalHomeServiceTests
{
    [Theory]
    [InlineData("2026-10-01", false)]
    [InlineData("2026-10-24", false)]
    [InlineData("2026-10-25", true)]
    [InlineData("2026-10-31", true)]
    [InlineData("2026-11-01", false)]
    [InlineData("2027-10-25", true)]
    public void Window_UsesInclusiveUtcDaysAndAnnualRecurrence(string now, bool active)
    {
        var expiry = SeasonalGenreWindow.GetExpiry(new(2025, 10, 25), new(2025, 10, 31), DateTime.Parse(now));
        Assert.Equal(active, expiry.HasValue);
        if (active)
        {
            Assert.Equal(new DateTime(DateTime.Parse(now).Year, 11, 1, 0, 0, 0, DateTimeKind.Utc), expiry);
            Assert.Equal(DateTimeKind.Utc, expiry!.Value.Kind);
        }
    }

    [Fact]
    public void Window_HandlesYearCrossingMissingAndInvalidBoundaries()
    {
        Assert.Equal(new DateTime(2027, 1, 7, 0, 0, 0, DateTimeKind.Utc),
            SeasonalGenreWindow.GetExpiry(new(2025, 12, 20), new(2026, 1, 6), new(2027, 1, 1)));
        Assert.Null(SeasonalGenreWindow.GetExpiry(null, new(2026, 10, 31), new(2026, 10, 25)));
        Assert.Null(SeasonalGenreWindow.GetExpiry(new(2026, 10, 31), null, new(2026, 10, 25)));
        Assert.Null(SeasonalGenreWindow.GetExpiry(new(2026, 11, 1), new(2026, 10, 31), new(2026, 10, 25)));
    }

    [Fact]
    public async Task Service_FiltersSourcesEmptyGenresAndMapsGroupedSortedWatchedMedia()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var user = new ApplicationUser { Id = "season-user", UserName = "season-user" };
        var allowed = new MediaSource { Name = "Allowed" };
        var denied = new MediaSource { Name = "Denied" };
        db.AddRange(user, allowed, denied);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.MediaSourceUsers.Add(new MediaSourceUser { UserId = user.Id, MediaSourceId = allowed.Id });
        var genre = new Genre { Name = "Halloween", MediaSourceId = allowed.Id, StartDate = new(2025, 10, 25), EndDate = new(2025, 10, 31) };
        var empty = new Genre { Name = "Empty", MediaSourceId = allowed.Id, StartDate = genre.StartDate, EndDate = genre.EndDate };
        var inactive = new Genre { Name = "Inactive", MediaSourceId = allowed.Id, StartDate = new(2025, 12, 1), EndDate = new(2025, 12, 31) };
        var multi = new MovieCollection { Name = "Collection", MediaSourceId = allowed.Id, ReleaseDate = new(2020, 1, 1) };
        var singleton = new MovieCollection { Name = "Singleton", MediaSourceId = allowed.Id };
        db.AddRange(genre, empty, inactive, multi, singleton);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var film = new Movie { Name = "Watched", MediaSourceId = allowed.Id, ReleaseDate = new(2024, 1, 1) };
        var member1 = new Movie { Name = "Member1", MediaSourceId = allowed.Id, MovieCollectionId = multi.Id };
        var member2 = new Movie { Name = "Member2", MediaSourceId = allowed.Id, MovieCollectionId = multi.Id };
        var single = new Movie { Name = "Only film", MediaSourceId = allowed.Id, MovieCollectionId = singleton.Id };
        var hidden = new Movie { Name = "Secret", MediaSourceId = denied.Id };
        var show = new TVShow { Name = "Show", MediaSourceId = allowed.Id, EndedAt = new(2026, 1, 1) };
        db.AddRange(film, member1, member2, single, hidden, show);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        foreach (var movie in new[] { film, member1, member2, single, hidden })
            db.MovieGenres.Add(new MovieGenre { GenreId = genre.Id, MovieId = movie.Id });
        db.TVShowGenres.Add(new TVShowGenre { GenreId = genre.Id, TVShowId = show.Id });
        db.WatchedEntries.Add(new WatchedEntry { UserId = user.Id, MovieId = film.Id, WatchedAt = new(2026, 10, 25) });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero));
        var service = new SeasonalHomeService(db, clock, new WatchedStatusService(db));
        var section = Assert.Single(await service.GetAsync(user.Id, TestContext.Current.CancellationToken));
        Assert.Equal(genre.Id, section.GenreId);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), section.ExpiresAt);
        Assert.Equal(new[] { "Show", "Watched", "Collection", "Only film" }, section.Entries.Select(e => e.Name));
        Assert.IsType<DtoTVShow>(section.Entries[0]);
        Assert.NotNull(Assert.IsType<DtoMovie>(section.Entries[1]).WatchedAt);
        Assert.IsType<DtoMovieCollection>(section.Entries[2]);
        Assert.IsType<DtoMovie>(section.Entries[3]);
        var json = JsonSerializer.Serialize(section, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"expiresAt\":\"2026-11-01T00:00:00Z\"", json);
        Assert.Contains("\"$type\":\"movieCollection\"", json);
        Assert.DoesNotContain("Secret", json);
        Assert.Empty(await service.GetAsync("other-user", TestContext.Current.CancellationToken));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(await service.GetAsync(user.Id, TestContext.Current.CancellationToken));
    }
}
