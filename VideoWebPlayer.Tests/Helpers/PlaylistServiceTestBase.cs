using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Provides a SQLite-in-memory-database-backed <see cref="PlaylistService"/> instance for tests.
/// </summary>
public abstract class PlaylistServiceTestBase : IDisposable
{
    protected readonly ApplicationDbContext _db;
    protected readonly PlaylistService _service;
    protected readonly string _testUserId = "test-user-123";
    protected readonly string _otherUserId = "other-user-456";
    protected readonly FakeAuthService _fakeAuthService;
    protected readonly IUnlockedMediaService _unlockedMediaService;
    private readonly SqliteConnection _keeperConnection;

    protected PlaylistServiceTestBase()
    {
        var connectionString = $"Data Source=file:playlist-service-{Guid.NewGuid()}?mode=memory&cache=shared";
        _keeperConnection = new SqliteConnection(connectionString);
        _keeperConnection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;
        _db = new ApplicationDbContext(options, new EventManager());
        _db.Database.EnsureCreated();

        var testUser = new ApplicationUser { Id = _testUserId, UserName = "test-user@test.com" };
        var otherUser = new ApplicationUser { Id = _otherUserId, UserName = "other-user@test.com" };
        _db.Users.AddRange(testUser, otherUser);
        _db.SaveChanges();

        _fakeAuthService = new FakeAuthService { CurrentUser = testUser };
        _unlockedMediaService = new UnlockedMediaService(_db, _fakeAuthService);

        _service = CreateService(null);
    }

    public void Dispose()
    {
        _keeperConnection.Dispose();
    }

    protected PlaylistService CreateService(int? maxPlaylistsPerUser = null, int? maxPlaylistItemCount = null)
    {
        var settings = Microsoft.Extensions.Options.Options.Create(new PlaylistSettings
        {
            MaxPlaylistsPerUser = maxPlaylistsPerUser,
            MaxPlaylistItemCount = maxPlaylistItemCount
        });
        return new PlaylistService(_db, _unlockedMediaService, settings);
    }

    /// <summary>
    /// Grants the given user unlocked access to a movie collection or TV show by inserting an
    /// <see cref="UnlockedMediaEntry"/> directly, bypassing <see cref="IUnlockedMediaService.SetUnlockedUsersAsync"/>.
    /// </summary>
    /// <param name="userId">The id of the user to grant unlocked access to.</param>
    /// <param name="mediaType">The media type of the target entity (e.g. movie collection or TV show).</param>
    /// <param name="mediaId">The id of the target entity.</param>
    protected async Task UnlockMediaForUserAsync(string userId, string mediaType, long mediaId)
    {
        _db.UnlockedMediaEntries.Add(UnlockedMediaTestHelper.CreateUnlockedMediaEntry(userId, mediaType, mediaId));
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Grants the given user regular access to the media source with the given id (defaulting to the
    /// <c>MediaSourceId = 1</c> used by <see cref="CreateTestMediaEntryAsync"/>) by inserting a
    /// <see cref="MediaSourceUser"/> entry, creating the referenced <see cref="MediaSource"/> first if
    /// it does not exist yet.
    /// </summary>
    /// <param name="userId">The id of the user to grant media-source access to.</param>
    /// <param name="mediaSourceId">The id of the media source to grant access to (created if missing).</param>
    protected async Task GrantMediaSourceAccessForUserAsync(string userId, long mediaSourceId = 1)
    {
        if (!await _db.MediaSources.AnyAsync(s => s.Id == mediaSourceId))
        {
            _db.MediaSources.Add(new MediaSource { Id = mediaSourceId, Name = "Test Source", Path = "/test", Host = "localhost", Port = 22 });
            await _db.SaveChangesAsync();
        }

        _db.MediaSourceUsers.Add(new MediaSourceUser { UserId = userId, MediaSourceId = mediaSourceId });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a real media entity of the given type (Movie, TVShow, TVShowSeason, TVShowEpisode or
    /// MovieCollection) directly in the database, creating parent entities as needed, and returns its id.
    /// </summary>
    /// <param name="mediaType">The media type to create (Movie, TVShow, TVShowSeason, TVShowEpisode or MovieCollection).</param>
    /// <param name="name">The name to give the created entity (and its generated parent entities, if any).</param>
    /// <returns>The id of the created media entity.</returns>
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
    /// Creates and persists a new playlist for the given user with a randomly generated name and the
    /// given sort mode (defaulting to sorted by release date). Shared by the various
    /// <c>CreateTestPlaylistWith*Async</c> helpers.
    /// </summary>
    /// <param name="userId">The id of the user who owns the playlist.</param>
    /// <param name="sortMode">The sort mode to create the playlist with.</param>
    /// <returns>The newly created and persisted <see cref="Playlist"/>.</returns>
    private async Task<Playlist> CreateTestPlaylistAsync(string userId, PlaylistSortMode sortMode = PlaylistSortMode.ByReleaseDate)
    {
        var playlist = new Playlist
        {
            UserId = userId,
            Name = $"Test-Playlist-{Guid.NewGuid()}",
            SortMode = sortMode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync();
        return playlist;
    }

    /// <summary>
    /// Creates a playlist in <see cref="PlaylistSortMode.Manual"/> mode for the given user and directly
    /// seeds it with the given (mediaType, mediaId, sortOrder) entries, bypassing the service. Returns
    /// the playlist id.
    /// </summary>
    /// <param name="userId">The id of the user who owns the playlist.</param>
    /// <param name="MediaType">The media type of an entry to seed.</param>
    /// <param name="MediaId">The media id of an entry to seed.</param>
    /// <param name="entries">The (MediaType, MediaId, SortOrder) tuples to seed as playlist entries.</param>
    /// <returns>The id of the created playlist.</returns>
    protected async Task<long> CreateTestManualPlaylistWithEntriesAsync(string userId, params (string MediaType, long MediaId, long? SortOrder)[] entries)
    {
        var playlist = await CreateTestPlaylistAsync(userId, PlaylistSortMode.Manual);

        foreach (var (mediaType, mediaId, sortOrder) in entries)
        {
            _db.PlaylistEntries.Add(new PlaylistEntry
            {
                PlaylistId = playlist.Id,
                MediaType = mediaType,
                MediaId = mediaId,
                SortOrder = sortOrder,
                AddedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        return playlist.Id;
    }

    /// <summary>
    /// Creates a playlist for the given user and directly seeds it with the given
    /// (mediaType, mediaId) entries, bypassing the service. Returns the playlist id.
    /// </summary>
    /// <param name="userId">The id of the user who owns the playlist.</param>
    /// <param name="MediaType">The media type of an entry to seed.</param>
    /// <param name="entries">The (MediaType, MediaId) tuples to seed as playlist entries.</param>
    /// <returns>The id of the created playlist.</returns>
    protected async Task<long> CreateTestPlaylistWithEntriesAsync(string userId, params (string MediaType, long MediaId)[] entries)
    {
        var playlist = await CreateTestPlaylistAsync(userId);

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

    /// <summary>
    /// Creates a playlist for the given user together with newly created Movie or MovieCollection
    /// media entries carrying the given release dates, added as top-level playlist entries.
    /// </summary>
    /// <param name="userId">The id of the user who owns the playlist.</param>
    /// <param name="MediaType">The media type of an entry to create.</param>
    /// <param name="Name">The name of the media entity to create for an entry.</param>
    /// <param name="entries">The (MediaType, Name, ReleaseDate) tuples describing the media entries to create and add.</param>
    /// <returns>The id of the created playlist.</returns>
    protected async Task<long> CreateTestPlaylistWithReleaseDatesAsync(string userId, params (string MediaType, string Name, DateTime? ReleaseDate)[] entries)
    {
        var playlist = await CreateTestPlaylistAsync(userId);

        foreach (var (mediaType, name, releaseDate) in entries)
        {
            long mediaId;
            switch (mediaType)
            {
                case MediaTypeValues.Movie:
                    var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, ReleaseDate = releaseDate };
                    _db.Movies.Add(movie);
                    await _db.SaveChangesAsync();
                    mediaId = movie.Id;
                    break;
                case MediaTypeValues.MovieCollection:
                    var collection = new MovieCollection { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, ReleaseDate = releaseDate };
                    _db.MovieCollections.Add(collection);
                    await _db.SaveChangesAsync();
                    mediaId = collection.Id;
                    break;
                default:
                    throw new ArgumentException($"Nicht unterstuetzter MediaType fuer Erscheinungsdatum-Test: {mediaType}", nameof(entries));
            }

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

    /// <summary>
    /// Creates a playlist (sorted <see cref="PlaylistSortMode.ByReleaseDate"/>) for the given user with a
    /// mix of playable entries (a movie and a TV show episode) and non-playable collection entries
    /// (TVShow, TVShowSeason, MovieCollection), for playback-navigation tests that must verify collection
    /// entries are skipped. Returns the playlist id and the ids of the created playlist entries, in
    /// insertion order (movie, TVShow, TVShowSeason, MovieCollection, episode).
    /// </summary>
    /// <param name="userId">The id of the user who owns the playlist.</param>
    /// <returns>The id of the created playlist and the ids of its entries, in insertion order.</returns>
    protected async Task<(long PlaylistId, long[] EntryIds)> CreateTestPlaylistWithMixedEntriesAsync(string userId)
    {
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film 1");
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Serie 1");
        var seasonId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShowSeason, "Staffel 1");
        var collectionId = await CreateTestMediaEntryAsync(MediaTypeValues.MovieCollection, "Sammlung 1");
        var episodeId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShowEpisode, "Episode 1");

        var playlistId = await CreateTestPlaylistWithEntriesAsync(userId,
            (MediaTypeValues.Movie, movieId),
            (MediaTypeValues.TVShow, showId),
            (MediaTypeValues.TVShowSeason, seasonId),
            (MediaTypeValues.MovieCollection, collectionId),
            (MediaTypeValues.TVShowEpisode, episodeId));

        var entryIds = await _db.PlaylistEntries.AsNoTracking()
            .Where(e => e.PlaylistId == playlistId)
            .OrderBy(e => e.AddedAt)
            .Select(e => e.Id)
            .ToArrayAsync();

        return (playlistId, entryIds);
    }

    /// <summary>
    /// Creates two playlists for the given user, each with three movies: one sorted
    /// <see cref="PlaylistSortMode.ByReleaseDate"/> (ordered by release date) and one sorted
    /// <see cref="PlaylistSortMode.Manual"/> (ordered by an explicit <see cref="PlaylistEntry.SortOrder"/>
    /// deliberately different from insertion order), for tests that must verify playback navigation
    /// honors the playlist's current sort mode rather than insertion order.
    /// </summary>
    /// <param name="userId">The id of the user who owns both playlists.</param>
    /// <returns>The ids of the created by-release-date and manual playlists.</returns>
    protected async Task<(long ByReleaseDatePlaylistId, long ManualPlaylistId)> CreatePlaylistWithMultipleSortOrdersAsync(string userId)
    {
        var byReleaseDatePlaylistId = await CreateTestPlaylistWithReleaseDatesAsync(userId,
            (MediaTypeValues.Movie, "Film A", new DateTime(2020, 1, 1)),
            (MediaTypeValues.Movie, "Film B", new DateTime(2021, 1, 1)),
            (MediaTypeValues.Movie, "Film C", new DateTime(2022, 1, 1)));

        var movieA = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Manuell A");
        var movieB = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Manuell B");
        var movieC = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Manuell C");
        var manualPlaylistId = await CreateTestManualPlaylistWithEntriesAsync(userId,
            (MediaTypeValues.Movie, movieC, 0),
            (MediaTypeValues.Movie, movieA, 1),
            (MediaTypeValues.Movie, movieB, 2));

        return (byReleaseDatePlaylistId, manualPlaylistId);
    }
}
