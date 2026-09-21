using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Base class for the tests around the playlist behavior of the continue-watching list (Korrektur
/// "Weiterschauen-Playlist"): a real <see cref="ContinueWatchingService"/> and a real
/// <see cref="PlaylistService"/> wired to each other on the shared, real-SQLite-backed
/// <see cref="PlaylistServiceTestBase._db"/>, so the successor resolution really runs through the
/// playlist's sort order, its exclusions and its accessibility rules - and so the unique indexes on
/// <c>ContinueWatchingEntries</c> (which EF InMemory would not enforce) are exercised.
/// </summary>
public abstract class ContinueWatchingPlaylistTestBase : PlaylistServiceTestBase
{
    // Gesamtlaenge der Fortschrittsmeldungen dieser Tests.
    protected static readonly TimeSpan Duration = TimeSpan.FromMinutes(45);

    // Position innerhalb der Endsequenz (Standardgrenze: letzte 30 Sekunden).
    protected static readonly TimeSpan EndSequencePosition = TimeSpan.FromMinutes(45) - TimeSpan.FromSeconds(10);

    // Position deutlich vor der Endsequenz.
    protected static readonly TimeSpan MidPosition = TimeSpan.FromMinutes(10);

    protected readonly PlaylistService _playlists;
    protected readonly ContinueWatchingService _continueWatching;
    protected readonly ContinueWatchingBuffer _buffer = new();

    protected ContinueWatchingPlaylistTestBase()
    {
        PlaylistService? playlistService = null;
        _continueWatching = BuildContinueWatchingService(() => playlistService!, _buffer);

        var services = new ServiceCollection();
        services.AddSingleton(_continueWatching);
        playlistService = CreateService(serviceProvider: services.BuildServiceProvider());

        _playlists = playlistService;
    }

    /// <summary>
    /// Drains the progress buffer the way <see cref="ContinueWatchingWorker"/> does (read the pending
    /// keys, process each snapshot), so a test can exercise the full
    /// <see cref="ContinueWatchingService.ReportProgressAsync"/> path rather than calling
    /// <see cref="ContinueWatchingService.ProcessBufferedEntryAsync"/> directly.
    /// </summary>
    /// <param name="expectedCount">How many buffered snapshots to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task FlushBufferAsync(int expectedCount, CancellationToken cancellationToken)
    {
        for (var i = 0; i < expectedCount; i++)
        {
            var entry = await _buffer.ReadNextAsync(cancellationToken);
            if (entry is null)
                continue;

            await _continueWatching.ProcessBufferedEntryAsync(
                entry.UserId, entry.MovieId, entry.EpisodeId, entry.Position, entry.Duration, entry.PlaylistId, cancellationToken);
        }
    }

    /// <summary>
    /// Creates a TV show with one season and the given number of episodes, numbered 1..n, on the media
    /// source <paramref name="mediaSourceId"/> (1 by default, the one
    /// <see cref="PlaylistServiceTestBase.GrantMediaSourceAccessForUserAsync"/> grants access to).
    /// </summary>
    /// <param name="showName">The name of the show.</param>
    /// <param name="episodeCount">How many episodes to create.</param>
    /// <param name="mediaSourceId">The media source to create the show on.</param>
    /// <returns>The created show id and its episode ids in ascending episode number.</returns>
    protected async Task<(long ShowId, long[] EpisodeIds)> CreateShowAsync(string showName, int episodeCount, long mediaSourceId = 1)
    {
        var show = new TVShow { Name = showName, MediaSourceId = mediaSourceId, CreatedAt = DateTime.UtcNow };
        _db.TVShows.Add(show);
        await _db.SaveChangesAsync();

        var season = new TVShowSeason { Name = $"{showName} Staffel 1", TVShowId = show.Id, MediaSourceId = mediaSourceId, CreatedAt = DateTime.UtcNow };
        _db.TVShowSeasons.Add(season);
        await _db.SaveChangesAsync();

        var episodeIds = new List<long>();
        for (var i = 1; i <= episodeCount; i++)
        {
            var episode = new TVShowEpisode
            {
                Name = $"{showName} E{i}",
                TVShowSeasonId = season.Id,
                MediaSourceId = mediaSourceId,
                CreatedAt = DateTime.UtcNow,
                Number = i
            };
            _db.TVShowEpisodes.Add(episode);
            await _db.SaveChangesAsync();
            episodeIds.Add(episode.Id);
        }

        return (show.Id, episodeIds.ToArray());
    }

    /// <summary>
    /// Creates a standalone movie on the given media source.
    /// </summary>
    /// <param name="name">The movie name.</param>
    /// <param name="mediaSourceId">The media source to create the movie on.</param>
    /// <returns>The created movie's id.</returns>
    protected async Task<long> CreateStandaloneMovieAsync(string name, long mediaSourceId = 1)
    {
        var movie = new Movie { Name = name, MediaSourceId = mediaSourceId, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie.Id;
    }

    /// <summary>
    /// Reads the continue-watching entries of a user bound to the given playlist.
    /// </summary>
    /// <param name="playlistId">The playlist the entries must be bound to.</param>
    /// <param name="userId">The owning user, defaulting to <see cref="PlaylistServiceTestBase._testUserId"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching entries.</returns>
    protected async Task<List<ContinueWatchingEntry>> GetContinueWatchingEntriesAsync(long playlistId, string? userId = null, CancellationToken cancellationToken = default)
        => await _db.ContinueWatchingEntries.AsNoTracking()
            .Where(x => x.UserId == (userId ?? _testUserId) && x.PlaylistId == playlistId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Reads the continue-watching entries of a user that carry no playlist binding at all.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="userId">The owning user, defaulting to <see cref="PlaylistServiceTestBase._testUserId"/>.</param>
    /// <returns>The matching entries.</returns>
    protected async Task<List<ContinueWatchingEntry>> GetPlaylistlessEntriesAsync(CancellationToken cancellationToken, string? userId = null)
        => await _db.ContinueWatchingEntries.AsNoTracking()
            .Where(x => x.UserId == (userId ?? _testUserId) && x.PlaylistId == null)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Builds a claims principal for <see cref="ContinueWatchingService.GetListAsync"/>; the user manager
    /// mock built by <see cref="PlaylistServiceTestBase.BuildContinueWatchingService"/> always resolves it
    /// to <see cref="PlaylistServiceTestBase._testUserId"/>.
    /// </summary>
    /// <returns>The principal to pass to the service.</returns>
    protected static System.Security.Claims.ClaimsPrincipal CreateTestPrincipal() => new();

    /// <summary>
    /// Looks up the id of the playlist entry referencing the given media within a playlist.
    /// </summary>
    /// <param name="playlistId">The playlist to look in.</param>
    /// <param name="mediaType">The media type of the referenced media.</param>
    /// <param name="mediaId">The id of the referenced media.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playlist entry's id.</returns>
    protected async Task<long> GetPlaylistEntryIdAsync(long playlistId, string mediaType, long mediaId, CancellationToken cancellationToken)
        => await _db.PlaylistEntries.AsNoTracking()
            .Where(e => e.PlaylistId == playlistId && e.MediaType == mediaType && e.MediaId == mediaId)
            .Select(e => e.Id)
            .SingleAsync(cancellationToken);

    /// <summary>
    /// Seeds a continue-watching entry directly in the database, bypassing the service, to arrange a
    /// pre-existing (possibly legacy/duplicated) state.
    /// </summary>
    /// <param name="movieId">The referenced movie id, or <see langword="null"/>.</param>
    /// <param name="episodeId">The referenced episode id, or <see langword="null"/>.</param>
    /// <param name="playlistId">The playlist to bind the entry to, or <see langword="null"/>.</param>
    /// <param name="userId">The owning user, defaulting to <see cref="PlaylistServiceTestBase._testUserId"/>.</param>
    /// <param name="position">The playback position to seed.</param>
    /// <returns>The created entry.</returns>
    protected async Task<ContinueWatchingEntry> SeedContinueWatchingEntryAsync(
        long? movieId, long? episodeId, long? playlistId, string? userId = null, TimeSpan? position = null)
    {
        var entry = new ContinueWatchingEntry
        {
            UserId = userId ?? _testUserId,
            MovieId = movieId,
            TVShowEpisodeId = episodeId,
            PlaylistId = playlistId,
            Position = position ?? TimeSpan.FromMinutes(1),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = DateTime.UtcNow.Ticks
        };
        _db.ContinueWatchingEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    /// <summary>
    /// Arranges the customer's first scenario: two TV shows (an original and its continuation) gathered
    /// in one manually sorted playlist, in the order A1, A2, B1, B2, with media-source access granted.
    /// </summary>
    /// <returns>The playlist id and both shows' episode ids.</returns>
    protected async Task<(long PlaylistId, long[] ShowAEpisodes, long[] ShowBEpisodes)> CreateTwoShowPlaylistAsync()
    {
        var (_, showAEpisodes) = await CreateShowAsync("Original", 2);
        var (_, showBEpisodes) = await CreateShowAsync("Fortsetzung", 2);
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, showAEpisodes[0], 0),
            (MediaTypeValues.TVShowEpisode, showAEpisodes[1], 1),
            (MediaTypeValues.TVShowEpisode, showBEpisodes[0], 2),
            (MediaTypeValues.TVShowEpisode, showBEpisodes[1], 3));

        return (playlistId, showAEpisodes, showBEpisodes);
    }
}
