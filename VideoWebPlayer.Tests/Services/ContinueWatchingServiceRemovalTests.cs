using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests für die playlist-gefilterte Entfernung bestehender <see cref="ContinueWatchingEntry"/>-Einträge
/// derselben Serie/Sammlung.
/// </summary>
public sealed class ContinueWatchingServiceRemovalTests : ContinueWatchingServiceTestBase
{
    [Fact]
    public async Task Playlist_RemoveExistingTVShow_OnlyRemovesMatchingPlaylistId()
    {
        var ct = TestContext.Current.CancellationToken;
        await TestHelpers.CreateTvShowWithSeasonsAsync(
            _db,
            ("Staffel 01", new (int, DateTime?)[] { (1, null), (2, null) }));
        var episode1 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 1, ct);
        var episode2 = await _db.TVShowEpisodes.FirstAsync(e => e.Number == 2, ct);

        var playlist1 = await CreateTestPlaylistAsync(_testUserId, "Playlist 1");
        var playlist2 = await CreateTestPlaylistAsync(_testUserId, "Playlist 2");
        await CreateTestPlaylistEntryAsync(playlist1.Id, episode1.Id, MediaTypeValues.TVShowEpisode);
        await CreateTestPlaylistEntryAsync(playlist1.Id, episode2.Id, MediaTypeValues.TVShowEpisode);
        await CreateTestPlaylistEntryAsync(playlist2.Id, episode1.Id, MediaTypeValues.TVShowEpisode);

        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            TVShowEpisodeId = episode1.Id,
            PlaylistId = playlist1.Id,
            Position = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        });
        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            TVShowEpisodeId = episode1.Id,
            PlaylistId = playlist2.Id,
            Position = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 2
        });
        await _db.SaveChangesAsync(ct);

        // Neuer Fortschritt fuer Episode 2 mit Playlist1 sollte NUR den Playlist1-Eintrag von Episode1 ersetzen.
        await _service.ProcessBufferedEntryAsync(_testUserId, null, episode2.Id, TimeSpan.FromMinutes(3), Duration, playlist1.Id, ct: ct);

        var remaining = await _db.ContinueWatchingEntries.Where(x => x.UserId == _testUserId).ToListAsync(ct);
        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, e => e.TVShowEpisodeId == episode2.Id && e.PlaylistId == playlist1.Id);
        Assert.Contains(remaining, e => e.TVShowEpisodeId == episode1.Id && e.PlaylistId == playlist2.Id);
    }

    [Fact]
    public async Task Playlist_RemoveExistingMovie_OnlyRemovesMatchingPlaylistId()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = new MovieCollection { Name = "Collection", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.MovieCollections.Add(collection);
        await _db.SaveChangesAsync(ct);

        var movie1 = new Movie { Name = "Movie 1", MovieCollectionId = collection.Id, ReleaseDate = new DateTime(2020, 1, 1), MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        var movie2 = new Movie { Name = "Movie 2", MovieCollectionId = collection.Id, ReleaseDate = new DateTime(2021, 1, 1), MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.Movies.AddRange(movie1, movie2);
        await _db.SaveChangesAsync(ct);

        var playlist1 = await CreateTestPlaylistAsync(_testUserId, "Playlist 1");
        var playlist2 = await CreateTestPlaylistAsync(_testUserId, "Playlist 2");
        await CreateTestPlaylistEntryAsync(playlist1.Id, movie1.Id, MediaTypeValues.Movie);
        await CreateTestPlaylistEntryAsync(playlist1.Id, movie2.Id, MediaTypeValues.Movie);
        await CreateTestPlaylistEntryAsync(playlist2.Id, movie1.Id, MediaTypeValues.Movie);

        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movie1.Id,
            PlaylistId = playlist1.Id,
            Position = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        });
        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movie1.Id,
            PlaylistId = playlist2.Id,
            Position = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 2
        });
        await _db.SaveChangesAsync(ct);

        // Neuer Fortschritt fuer Movie 2 mit Playlist1 sollte NUR den Playlist1-Eintrag von Movie1 ersetzen.
        await _service.ProcessBufferedEntryAsync(_testUserId, movie2.Id, null, TimeSpan.FromMinutes(3), Duration, playlist1.Id, ct: ct);

        var remaining = await _db.ContinueWatchingEntries.Where(x => x.UserId == _testUserId).ToListAsync(ct);
        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, e => e.MovieId == movie2.Id && e.PlaylistId == playlist1.Id);
        Assert.Contains(remaining, e => e.MovieId == movie1.Id && e.PlaylistId == playlist2.Id);
    }
}
