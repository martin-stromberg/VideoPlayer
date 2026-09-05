using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Provides an in-memory-database-backed <see cref="PlaylistService"/> instance for tests.
/// </summary>
public abstract class PlaylistServiceTestBase
{
    protected readonly ApplicationDbContext _db;
    protected readonly EventManager _eventManager;
    protected readonly PlaylistService _service;
    protected readonly string _testUserId = "test-user-123";
    protected readonly string _otherUserId = "other-user-456";

    protected PlaylistServiceTestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _eventManager = new EventManager();
        _db = new ApplicationDbContext(options, _eventManager);
        _service = CreateService(null);
    }

    protected PlaylistService CreateService(int? maxPlaylistsPerUser = null, int? maxPlaylistItemCount = null)
    {
        var settings = Microsoft.Extensions.Options.Options.Create(new PlaylistSettings
        {
            MaxPlaylistsPerUser = maxPlaylistsPerUser,
            MaxPlaylistItemCount = maxPlaylistItemCount
        });
        return new PlaylistService(_db, _eventManager, settings);
    }

    /// <summary>
    /// Creates a real media entity of the given type (Movie, TVShow, TVShowSeason, TVShowEpisode or
    /// MovieCollection) directly in the database, creating parent entities as needed, and returns its id.
    /// </summary>
    protected async Task<long> CreateTestMediaEntryAsync(string mediaType, string name = "Test Media")
    {
        switch (mediaType)
        {
            case MediaTypeValues.Movie:
                {
                    var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
                    _db.Movies.Add(movie);
                    await _db.SaveChangesAsync();
                    return movie.Id;
                }
            case MediaTypeValues.MovieCollection:
                {
                    var collection = new MovieCollection { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
                    _db.MovieCollections.Add(collection);
                    await _db.SaveChangesAsync();
                    return collection.Id;
                }
            case MediaTypeValues.TVShow:
                {
                    var show = new TVShow { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
                    _db.TVShows.Add(show);
                    await _db.SaveChangesAsync();
                    return show.Id;
                }
            case MediaTypeValues.TVShowSeason:
                {
                    var show = new TVShow { Name = $"{name} Show", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
                    _db.TVShows.Add(show);
                    await _db.SaveChangesAsync();
                    var season = new TVShowSeason { Name = name, TVShowId = show.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
                    _db.TVShowSeasons.Add(season);
                    await _db.SaveChangesAsync();
                    return season.Id;
                }
            case MediaTypeValues.TVShowEpisode:
                {
                    var show = new TVShow { Name = $"{name} Show", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
                    _db.TVShows.Add(show);
                    await _db.SaveChangesAsync();
                    var season = new TVShowSeason { Name = $"{name} Season", TVShowId = show.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
                    _db.TVShowSeasons.Add(season);
                    await _db.SaveChangesAsync();
                    var episode = new TVShowEpisode { Name = name, TVShowSeasonId = season.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
                    _db.TVShowEpisodes.Add(episode);
                    await _db.SaveChangesAsync();
                    return episode.Id;
                }
            default:
                throw new ArgumentException($"Unbekannter MediaType: {mediaType}", nameof(mediaType));
        }
    }

    /// <summary>
    /// Creates a playlist for the given user and directly seeds it with the given
    /// (mediaType, mediaId) entries, bypassing the service. Returns the playlist id.
    /// </summary>
    protected async Task<long> CreateTestPlaylistWithEntriesAsync(string userId, params (string MediaType, long MediaId)[] entries)
    {
        var playlist = new Playlist
        {
            UserId = userId,
            Name = $"Test-Playlist-{Guid.NewGuid()}",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync();

        foreach (var (mediaType, mediaId) in entries)
        {
            _db.PlaylistEntries.Add(new PlaylistEntry
            {
                PlaylistId = playlist.Id,
                MediaType = mediaType,
                MediaId = mediaId,
                AddedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        return playlist.Id;
    }
}
