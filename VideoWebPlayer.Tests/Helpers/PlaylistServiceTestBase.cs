using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Hubs;
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
    protected readonly string _connectionString;

    protected PlaylistServiceTestBase()
    {
        var connectionString = $"Data Source=file:playlist-service-{Guid.NewGuid()}?mode=memory&cache=shared";
        _connectionString = connectionString;
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

    protected PlaylistService CreateService(int? maxPlaylistsPerUser = null, int? maxPlaylistItemCount = null, IServiceProvider? serviceProvider = null)
    {
        var settings = Microsoft.Extensions.Options.Options.Create(new PlaylistSettings
        {
            MaxPlaylistsPerUser = maxPlaylistsPerUser,
            MaxPlaylistItemCount = maxPlaylistItemCount
        });
        return new PlaylistService(_db, _unlockedMediaService, settings, serviceProvider);
    }

    /// <summary>
    /// Builds a <see cref="PlaylistBackfillService"/> instance sharing this test's <see cref="_db"/>, for
    /// tests of the automatic playlist backfill mechanism (Entwicklungsschritt 8).
    /// </summary>
    /// <param name="maxPlaylistItemCount">The configured maximum playlist item count, or <c>null</c> for unlimited.</param>
    /// <returns>The constructed <see cref="PlaylistBackfillService"/> instance.</returns>
    internal PlaylistBackfillService CreateBackfillService(int? maxPlaylistItemCount = null)
    {
        var settings = Microsoft.Extensions.Options.Options.Create(new PlaylistSettings
        {
            MaxPlaylistItemCount = maxPlaylistItemCount
        });
        return new PlaylistBackfillService(_db, settings);
    }

    /// <summary>
    /// Reads the pending playlist backfill markers straight from the database (no tracking).
    /// </summary>
    /// <returns>The markers, ordered by media type and id.</returns>
    protected Task<List<PlaylistBackfillMarker>> GetMarkersAsync()
        => _db.PlaylistBackfillMarkers.AsNoTracking().OrderBy(m => m.MediaType).ThenBy(m => m.MediaId).ToListAsync();

    /// <summary>
    /// Removes all pending backfill markers, so a test can start from "nothing marked" after arranging its data.
    /// </summary>
    /// <returns>A task completing when the markers are removed.</returns>
    protected Task ClearMarkersAsync()
        => _db.PlaylistBackfillMarkers.ExecuteDeleteAsync();

    /// <summary>
    /// Runs one marker-driven backfill pass the way <see cref="PlaylistBackfillCoordinator"/> does: plans the
    /// pending markers, backfills exactly the affected playlists in one block, and releases the markers.
    /// </summary>
    /// <param name="backfillService">The backfill service under test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The block result.</returns>
    internal async Task<PlaylistBackfillBlockResult> RunPendingBackfillAsync(PlaylistBackfillService backfillService, CancellationToken cancellationToken)
    {
        var plan = await backfillService.PlanPendingAsync(cancellationToken);
        var result = await backfillService.BackfillPlaylistsAsync(plan.PlaylistIds, cancellationToken);
        await backfillService.ReleaseMarkersAsync(plan, result.FailedPlaylistIds, cancellationToken);
        return result;
    }

    /// <summary>
    /// Creates a TV show with a single season and the given number of episodes directly in the database
    /// (bypassing the service), for backfill tests that need to add further seasons/episodes to an
    /// already-existing show afterwards. Returns the show, season and episode ids.
    /// </summary>
    /// <param name="showName">The name of the TV show to create.</param>
    /// <param name="seasonName">The name of the season to create.</param>
    /// <param name="episodeCount">How many episodes to create in the season.</param>
    /// <returns>The created show id, season id, and the created episode ids in creation order.</returns>
    protected async Task<(long ShowId, long SeasonId, long[] EpisodeIds)> CreateShowWithSeasonAsync(
        string showName, string seasonName, int episodeCount)
    {
        var show = new TVShow { Name = showName, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.TVShows.Add(show);
        await _db.SaveChangesAsync();

        var (seasonId, episodeIds) = await AddSeasonToShowAsync(show.Id, seasonName, episodeCount);
        return (show.Id, seasonId, episodeIds);
    }

    /// <summary>
    /// Adds a further season with the given number of episodes to an already-existing TV show directly in
    /// the database (bypassing the service), simulating a new season/episodes appearing in the media
    /// library after the playlist was originally populated.
    /// </summary>
    /// <param name="showId">The id of the already-existing TV show.</param>
    /// <param name="seasonName">The name of the season to create.</param>
    /// <param name="episodeCount">How many episodes to create in the season.</param>
    /// <returns>The created season id, and the created episode ids in creation order.</returns>
    protected async Task<(long SeasonId, long[] EpisodeIds)> AddSeasonToShowAsync(long showId, string seasonName, int episodeCount)
    {
        var season = new TVShowSeason { Name = seasonName, TVShowId = showId, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.TVShowSeasons.Add(season);
        await _db.SaveChangesAsync();

        var episodeIds = new List<long>();
        for (var i = 0; i < episodeCount; i++)
        {
            var episode = new TVShowEpisode { Name = $"{seasonName}-Episode-{i + 1}", TVShowSeasonId = season.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, Number = i + 1 };
            _db.TVShowEpisodes.Add(episode);
            await _db.SaveChangesAsync();
            episodeIds.Add(episode.Id);
        }

        return (season.Id, episodeIds.ToArray());
    }

    /// <summary>
    /// Adds a further movie to an already-existing movie collection directly in the database (bypassing the
    /// service), simulating a new movie appearing in the media library after the playlist was originally
    /// populated. Analogous to <see cref="AddSeasonToShowAsync"/> for TV shows.
    /// </summary>
    /// <param name="collectionId">The id of the already-existing movie collection.</param>
    /// <param name="name">The name to give the created movie.</param>
    /// <returns>The created movie id.</returns>
    protected async Task<long> AddMovieToCollectionAsync(long collectionId, string name)
    {
        var movie = new Movie { Name = name, MediaSourceId = 1, MovieCollectionId = collectionId, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie.Id;
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

    private readonly Dictionary<string, long> _genreIdsByName = new(StringComparer.OrdinalIgnoreCase);
    private bool _mediaSourceEnsured;

    /// <summary>
    /// Returns the id of a <see cref="Genre"/> row named <paramref name="name"/>, creating it (and, the
    /// first time, its required owning <see cref="MediaSource"/> row - these tests run against real
    /// SQLite with foreign keys enforced, unlike some other tests backed by EF Core's in-memory provider)
    /// if it does not exist yet. Reused across calls within one test so requesting the same genre name
    /// twice returns the same id, for genre-derivation tests (Entwicklungsschritt 9).
    /// </summary>
    /// <param name="name">The genre name.</param>
    /// <returns>The genre's id.</returns>
    protected async Task<long> GetOrCreateGenreIdAsync(string name)
    {
        if (_genreIdsByName.TryGetValue(name, out var existingId))
            return existingId;

        if (!_mediaSourceEnsured)
        {
            if (!await _db.MediaSources.AnyAsync(s => s.Id == 1))
                _db.MediaSources.Add(new MediaSource { Id = 1, Name = "Test Source", Path = "/test", Host = "localhost", Port = 22 });
            await _db.SaveChangesAsync();
            _mediaSourceEnsured = true;
        }

        var genre = new Genre { MediaSourceId = 1, Name = name };
        _db.Genres.Add(genre);
        await _db.SaveChangesAsync();
        _genreIdsByName[name] = genre.Id;
        return genre.Id;
    }

    /// <summary>
    /// Creates a standalone movie directly in the database (bypassing the service) carrying the given
    /// genres (created via <see cref="GetOrCreateGenreIdAsync"/> if not already known).
    /// </summary>
    /// <param name="name">The name of the movie to create.</param>
    /// <param name="genreNames">The genre names to assign to the movie.</param>
    /// <returns>The created movie's id.</returns>
    protected Task<long> CreateMovieWithGenresAsync(string name, params string[] genreNames)
        => CreateMovieWithGenresCoreAsync(name, null, genreNames);

    /// <summary>
    /// Creates a movie as part of an already-existing movie collection directly in the database (bypassing
    /// the service), carrying the given genres (created via <see cref="GetOrCreateGenreIdAsync"/> if not
    /// already known).
    /// </summary>
    /// <param name="movieCollectionId">The id of the already-existing movie collection.</param>
    /// <param name="name">The name of the movie to create.</param>
    /// <param name="genreNames">The genre names to assign to the movie.</param>
    /// <returns>The created movie's id.</returns>
    protected Task<long> CreateMovieInCollectionWithGenresAsync(long movieCollectionId, string name, params string[] genreNames)
        => CreateMovieWithGenresCoreAsync(name, movieCollectionId, genreNames);

    private async Task<long> CreateMovieWithGenresCoreAsync(string name, long? movieCollectionId, string[] genreNames)
    {
        var movie = new Movie { Name = name, MediaSourceId = 1, MovieCollectionId = movieCollectionId, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();

        foreach (var genreName in genreNames)
        {
            var genreId = await GetOrCreateGenreIdAsync(genreName);
            _db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genreId });
        }
        await _db.SaveChangesAsync();

        return movie.Id;
    }

    /// <summary>
    /// Assigns the given genres (created via <see cref="GetOrCreateGenreIdAsync"/> if not already known)
    /// to an already-existing TV show.
    /// </summary>
    /// <param name="showId">The id of the TV show to assign genres to.</param>
    /// <param name="genreNames">The genre names to assign.</param>
    protected async Task AddGenresToTVShowAsync(long showId, params string[] genreNames)
    {
        foreach (var genreName in genreNames)
        {
            var genreId = await GetOrCreateGenreIdAsync(genreName);
            _db.TVShowGenres.Add(new TVShowGenre { TVShowId = showId, GenreId = genreId });
        }
        await _db.SaveChangesAsync();
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
                    throw new ArgumentException($"Nicht unterstützter MediaType für Erscheinungsdatum-Test: {mediaType}", nameof(entries));
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

    /// <summary>
    /// Builds a <see cref="PlaylistService"/> wired to a real <see cref="ContinueWatchingService"/> (via a
    /// minimal <see cref="IServiceProvider"/>, mirroring how the production DI container resolves it lazily
    /// to avoid the <see cref="PlaylistService"/>-&gt;<see cref="ContinueWatchingService"/>-&gt;<see cref="IPlaylistService"/>
    /// circular dependency), so continue-watching-affecting <see cref="PlaylistService"/> operations
    /// (<see cref="PlaylistService.DeletePlaylistAsync"/>, <see cref="PlaylistService.RemoveMediaFromPlaylistAsync"/>,
    /// the silent orphan cleanup inside entry-loading methods) actually exercise their
    /// <see cref="ContinueWatchingService"/> counterparts against the shared, real-SQLite-backed
    /// <see cref="_db"/>. <see cref="_service"/> (built by the constructor without a service provider)
    /// intentionally does not perform this - callers that need it use a separate, dedicated instance from
    /// this method instead.
    /// </summary>
    /// <returns>The wired <see cref="PlaylistService"/> instance.</returns>
    protected PlaylistService CreatePlaylistServiceWithConflictResolution()
    {
        PlaylistService? playlistService = null;

        var services = new ServiceCollection();
        services.AddSingleton(_ => BuildContinueWatchingService(() => playlistService!));
        var serviceProvider = services.BuildServiceProvider();

        playlistService = CreateService(serviceProvider: serviceProvider);
        return playlistService;
    }

    /// <summary>
    /// Marks the given playlist as public directly in the database (bypassing the administrator check of
    /// <see cref="PlaylistService.SetPlaylistPublicAsync"/>), for tests of the read-only viewer behavior
    /// of public playlists (Entwicklungsschritt 11).
    /// </summary>
    /// <param name="playlistId">The id of the playlist to mark as public.</param>
    protected async Task MakePlaylistPublicAsync(long playlistId)
    {
        var playlist = await _db.Playlists.SingleAsync(p => p.Id == playlistId);
        playlist.IsPublic = true;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Builds a real <see cref="ContinueWatchingService"/> (see <see cref="CreatePlaylistServiceWithConflictResolution"/>)
    /// on the shared real-SQLite <see cref="_db"/>, resolving its <see cref="IPlaylistService"/> lazily.
    /// </summary>
    /// <param name="resolvePlaylistService">Resolves the playlist service on demand.</param>
    /// <param name="buffer">
    /// The progress buffer to wire the service to, so a test can drive the full
    /// <c>ReportProgressAsync</c> -&gt; buffer -&gt; <c>ProcessBufferedEntryAsync</c> path the
    /// <see cref="ContinueWatchingWorker"/> runs in production; a fresh, private buffer when omitted.
    /// </param>
    /// <param name="onGetNextPlaylistEntry">
    /// Invoked whenever the wired service asks the playlist service for the next playlist entry, so a test can
    /// count how often the (expensive) playlist load and accessibility resolution really happens.
    /// </param>
    /// <returns>The continue-watching service.</returns>
    protected ContinueWatchingService BuildContinueWatchingService(
        Func<IPlaylistService> resolvePlaylistService, ContinueWatchingBuffer? buffer = null, Action? onGetNextPlaylistEntry = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManagerMock
            .Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = _testUserId, UserName = _testUserId });

        var mockClientProxy = new Mock<IClientProxy>();
        var mockClients = new Mock<IHubClients>();
        mockClients.Setup(x => x.User(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockClients.Setup(x => x.All).Returns(mockClientProxy.Object);
        var mockHubContext = new Mock<IHubContext<MediaUpdateHub>>();
        mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        var notificationService = new MediaUpdateNotificationService(mockHubContext.Object, Mock.Of<ILogger<MediaUpdateNotificationService>>());

        var programSettings = new ProgramSettingsService(_db, Mock.Of<ILogger<ProgramSettingsService>>());

        return new ContinueWatchingService(
            _db,
            userManagerMock.Object,
            Mock.Of<ILogger<ContinueWatchingService>>(),
            buffer ?? new ContinueWatchingBuffer(),
            notificationService,
            programSettings,
            new LazyPlaylistService(resolvePlaylistService, onGetNextPlaylistEntry));
    }

    /// <summary>
    /// Forwards every <see cref="IPlaylistService"/> call to the instance <paramref name="resolve"/>
    /// returns, resolved lazily on each call rather than once up front: <see cref="ContinueWatchingService"/>
    /// needs an <see cref="IPlaylistService"/> at construction time, but the real <see cref="PlaylistService"/>
    /// instance (also needing this <see cref="ContinueWatchingService"/>, resolved lazily via
    /// <see cref="IServiceProvider"/>) is only assigned after this constructor call returns.
    /// </summary>
    private sealed class LazyPlaylistService : IPlaylistService
    {
        private readonly Func<IPlaylistService> _resolve;
        private readonly Action? _onGetNextPlaylistEntry;

        public LazyPlaylistService(Func<IPlaylistService> resolve, Action? onGetNextPlaylistEntry = null)
        {
            _resolve = resolve;
            _onGetNextPlaylistEntry = onGetNextPlaylistEntry;
        }

        public Task<DtoPlaylist[]> GetPlaylistsAsync(string userId, long? genreId = null, CancellationToken cancellationToken = default)
            => _resolve().GetPlaylistsAsync(userId, genreId, cancellationToken);

        public Task<DtoPlaylist[]> GetPublicPlaylistsAsync(string userId, long? genreId = null, CancellationToken cancellationToken = default)
            => _resolve().GetPublicPlaylistsAsync(userId, genreId, cancellationToken);

        public Task<DtoPlaylist> SetPlaylistPublicAsync(long playlistId, string userId, bool requesterIsAdmin, bool isPublic, CancellationToken cancellationToken = default)
            => _resolve().SetPlaylistPublicAsync(playlistId, userId, requesterIsAdmin, isPublic, cancellationToken);

        public Task<DtoPlaylist?> GetPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().GetPlaylistAsync(playlistId, userId, cancellationToken);

        public Task<DtoPlaylist> CreatePlaylistAsync(string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)
            => _resolve().CreatePlaylistAsync(userId, name, description, sortMode, cancellationToken);

        public Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)
            => _resolve().UpdatePlaylistAsync(playlistId, userId, name, description, sortMode, cancellationToken);

        public Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().DeletePlaylistAsync(playlistId, userId, cancellationToken);

        public Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
            => _resolve().AddMediaToPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken);

        public Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, bool confirmContinueWatchingRemoval = false, CancellationToken cancellationToken = default)
            => _resolve().RemoveMediaFromPlaylistAsync(playlistId, userId, mediaType, mediaId, confirmContinueWatchingRemoval, cancellationToken);

        public Task<DtoPlaylistEntry[]> GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().GetPlaylistEntriesAsync(playlistId, userId, cancellationToken);

        public Task<DtoPlaylistEntriesPagedResult> GetPlaylistEntriesPagedAsync(long playlistId, string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => _resolve().GetPlaylistEntriesPagedAsync(playlistId, userId, pageNumber, pageSize, cancellationToken);

        public Task ReorderPlaylistEntryAsync(long playlistId, string userId, long entryId, long newSortOrder, CancellationToken cancellationToken = default)
            => _resolve().ReorderPlaylistEntryAsync(playlistId, userId, entryId, newSortOrder, cancellationToken);

        public Task<DtoPlaylistEntry[]> BatchReorderPlaylistEntriesAsync(long playlistId, string userId, List<(long EntryId, long NewSortOrder)> reorderOperations, CancellationToken cancellationToken = default)
            => _resolve().BatchReorderPlaylistEntriesAsync(playlistId, userId, reorderOperations, cancellationToken);

        public Task<long?> GetMaxSortOrderAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().GetMaxSortOrderAsync(playlistId, userId, cancellationToken);

        public Task<DtoPlaylistEntry> MoveEntryToBeginningAsync(long playlistId, string userId, long entryId, CancellationToken cancellationToken = default)
            => _resolve().MoveEntryToBeginningAsync(playlistId, userId, entryId, cancellationToken);

        public Task MoveEntryBetweenAsync(long playlistId, string userId, long entryId, long targetSortOrder, CancellationToken cancellationToken = default)
            => _resolve().MoveEntryBetweenAsync(playlistId, userId, entryId, targetSortOrder, cancellationToken);

        public Task<DtoPlaylist> ChangeSortModeAsync(long playlistId, string userId, string newSortMode, bool? confirmLossOfManualOrder, CancellationToken cancellationToken = default)
            => _resolve().ChangeSortModeAsync(playlistId, userId, newSortMode, confirmLossOfManualOrder, cancellationToken);

        public Task<DtoPlaylist> SetPlaylistGenresAsync(long playlistId, string userId, long[] genreIds, CancellationToken cancellationToken = default)
            => _resolve().SetPlaylistGenresAsync(playlistId, userId, genreIds, cancellationToken);

        public Task<DtoPlaylist> ResetPlaylistGenresAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().ResetPlaylistGenresAsync(playlistId, userId, cancellationToken);

        public Task<DtoPlaylistNavigationResult?> GetNextPlaylistEntryAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default)
        {
            _onGetNextPlaylistEntry?.Invoke();
            return _resolve().GetNextPlaylistEntryAsync(playlistId, userId, currentEntryId, cancellationToken);
        }

        public Task<DtoPlaylistNavigationResult?> GetPreviousPlaylistEntryAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default)
            => _resolve().GetPreviousPlaylistEntryAsync(playlistId, userId, currentEntryId, cancellationToken);

        public Task<DtoPlaylistNavigationResult?> AdvancePlaylistAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default)
            => _resolve().AdvancePlaylistAsync(playlistId, userId, currentEntryId, cancellationToken);

        public Task<DtoPlaylistPlaybackStart> StartPlaylistAsync(long playlistId, string userId, long? entryId, CancellationToken cancellationToken = default)
            => _resolve().StartPlaylistAsync(playlistId, userId, entryId, cancellationToken);

        public Task ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(long mediaSourceId, CancellationToken cancellationToken = default)
            => _resolve().ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(mediaSourceId, cancellationToken);

        public Task<long?> GeneratePlaylistCoverAsync(long playlistId, string userId, bool confirmReplaceUploadedCover = false, CancellationToken cancellationToken = default)
            => _resolve().GeneratePlaylistCoverAsync(playlistId, userId, confirmReplaceUploadedCover, cancellationToken);

        public Task<byte[]?> PreviewPlaylistCoverAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().PreviewPlaylistCoverAsync(playlistId, userId, cancellationToken);

        public Task<long> SetPlaylistCoverAsync(long playlistId, string userId, byte[] pictureData, string? contentType, CancellationToken cancellationToken = default)
            => _resolve().SetPlaylistCoverAsync(playlistId, userId, pictureData, contentType, cancellationToken);

        public Task<Picture?> GetPlaylistCoverAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().GetPlaylistCoverAsync(playlistId, userId, cancellationToken);

        public Task DeletePlaylistCoverAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().DeletePlaylistCoverAsync(playlistId, userId, cancellationToken);
    }
}
