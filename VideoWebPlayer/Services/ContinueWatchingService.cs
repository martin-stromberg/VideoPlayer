using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Provides access to continue-watching entries and buffering logic.
    /// </summary>
    public class ContinueWatchingService
    {
        private static readonly TimeSpan MinStart = TimeSpan.FromSeconds(5);

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ContinueWatchingService> _logger;
        private readonly ContinueWatchingBuffer _buffer;
        private readonly MediaUpdateNotificationService _notificationService;
        private readonly ProgramSettingsService _programSettings;
        private readonly IPlaylistService _playlistService;
        private readonly WatchedStatusService _watchedStatusService;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Represents the result of a manual continue-watching skip operation.
        /// </summary>
        public enum SkipResult
        {
            /// <summary>The requested entry does not exist for the user.</summary>
            NotFound,
            /// <summary>The entry was replaced by the next media item.</summary>
            Replaced,
            /// <summary>The entry was removed because no following media item exists.</summary>
            RemovedWithoutNext
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContinueWatchingService"/> class.
        /// </summary>
        /// <param name="db">Application database context.</param>
        /// <param name="userManager">User manager for identity lookups.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="buffer">In-memory buffer for progress entries.</param>
        /// <param name="notificationService">Service for sending SignalR notifications.</param>
        /// <param name="programSettings">Service for program-wide settings.</param>
        /// <param name="playlistService">Service used to validate playlist ownership.</param>
        /// <param name="watchedStatusService">Optional watched-status service.</param>
        /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
        public ContinueWatchingService(ApplicationDbContext db,
                                       UserManager<ApplicationUser> userManager,
                                       ILogger<ContinueWatchingService> logger,
                                       ContinueWatchingBuffer buffer,
                                       MediaUpdateNotificationService notificationService,
                                       ProgramSettingsService programSettings,
                                       IPlaylistService playlistService,
                                       WatchedStatusService? watchedStatusService = null,
                                       TimeProvider? timeProvider = null)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _buffer = buffer;
            _notificationService = notificationService;
            _programSettings = programSettings;
            _playlistService = playlistService;
            _watchedStatusService = watchedStatusService ?? new WatchedStatusService(db);
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Creates a DTO by copying matching properties from a source object.
        /// </summary>
        /// <typeparam name="T">The DTO type.</typeparam>
        /// <param name="ms">The source model instance.</param>
        /// <returns>The populated DTO.</returns>
        protected T Create<T>(object ms)
        {
            var sourceType = ms.GetType();
            var record = Activator.CreateInstance<T>();
            foreach (var prop in typeof(T).GetProperties().Where(p => !p.GetCustomAttributes(typeof(IgnoreAssignPropertyAttribute), false).Any()))
            {
                var sourceProp = sourceType.GetProperty(prop.Name);
                if (sourceProp != null && sourceProp.CanRead)
                {
                    var value = sourceProp.GetValue(ms);
                    prop.SetValue(record, value);
                }
            }
            return record;
        }

        /// <summary>
        /// Gets the continue-watching list for the specified user.
        /// </summary>
        /// <param name="user">The user principal.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The list of continue-watching entries.</returns>
        public async Task<List<ContinueWatchingDto>> GetListAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            var userId = await GetUserIdAsync(user, ct);
            if (userId == null) return new();

            var list = (await _db.ContinueWatchingEntries
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ListOrder)
                .ThenByDescending(x => x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .Take(50)
                .ToListAsync(ct))
                .Select(x => new ContinueWatchingDto
                {
                    MediaType = x.MovieId != null ? "movie" : "episode",
                    Entry = (x.MovieId != null) ? (_db.Movies.Where(m => m.Id == x.MovieId).ToList().Select(m =>
                    {
                        var movie = Create<DtoMovie>(m);
                        movie.Collection = Create<DtoMovieCollection>(_db.MovieCollections.Where(mc => mc.Id == m.MovieCollectionId).FirstOrDefault());
                        return movie;
                    }).FirstOrDefault()) : _db.TVShowEpisodes.Where(m => m.Id == x.TVShowEpisodeId).ToList().Select(e =>
                    {
                        var episode = Create<DtoTVShowEpisode>(e);
                        episode.Season = _db.TVShowSeasons.Where(mc => mc.Id == e.TVShowSeasonId).ToList().Select(s =>
                        {
                            var season = Create<DtoTVShowSeason>(s);
                            season.Show = Create<DtoTVShow>(_db.TVShows.Where(mc => mc.Id == s.TVShowId).FirstOrDefault());
                            return season;
                        }).FirstOrDefault();
                        episode.Name = $"{episode.Season.Show.Name} {episode.Season.Name} {episode.Number}: {episode.Name}";
                        return episode;
                    }).FirstOrDefault(),
                    PositionSeconds = (long)x.Position.TotalSeconds,
                    DurationSeconds = x.Duration.HasValue ? (long?)x.Duration.Value.TotalSeconds : null,
                    PlaylistId = x.PlaylistId,
                })
                .Select(t =>
                {
                    t.PosterPictureId = t.Entry?.PosterPictureId ?? t.Entry?.FanartPictureId;
                    t.Title = t.Entry?.Name ?? t.Title;
                    t.WatchedAt = t.Entry?.WatchedAt;
                    return t;
                })
                .ToList();

            await _watchedStatusService.EnrichAsync(userId, list.Select(x => x.Entry), ct);
            foreach (var item in list)
                item.WatchedAt = item.Entry?.WatchedAt;

            await EnrichPlaylistInfoAsync(list, ct);

            return list;
        }

        /// <summary>
        /// Populates <see cref="ContinueWatchingDto.PlaylistName"/> and <see cref="ContinueWatchingDto.PlaylistEntryId"/>
        /// for every entry with a <see cref="ContinueWatchingDto.PlaylistId"/>. <see cref="ContinueWatchingDto.PlaylistEntryId"/>
        /// is resolved by looking up the <see cref="PlaylistEntry"/> that currently references the same media within that
        /// playlist (unique per playlist by <c>MediaType</c>+<c>MediaId</c>), which lets
        /// <c>ContinueWatchingList.razor</c> reconstruct the exact <c>PlaylistPlaybackContext</c> position when resuming
        /// playback (<c>/playlists/{PlaylistId}?entryId={PlaylistEntryId}</c>) instead of only navigating to the playlist
        /// overview. Left <c>null</c> when the media is no longer part of the playlist, in which case the caller falls
        /// back to navigating without an <c>entryId</c>.
        /// </summary>
        /// <param name="list">The continue-watching DTOs to enrich, in place.</param>
        /// <param name="ct">A cancellation token.</param>
        private async Task EnrichPlaylistInfoAsync(List<ContinueWatchingDto> list, CancellationToken ct)
        {
            var playlistIds = list.Where(x => x.PlaylistId.HasValue).Select(x => x.PlaylistId!.Value).Distinct().ToList();
            if (playlistIds.Count == 0)
                return;

            var playlistNames = await _db.Playlists
                .AsNoTracking()
                .Where(p => playlistIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

            var playlistEntryIds = await _db.PlaylistEntries
                .AsNoTracking()
                .Where(pe => playlistIds.Contains(pe.PlaylistId))
                .Select(pe => new { pe.PlaylistId, pe.MediaType, pe.MediaId, pe.Id })
                .ToListAsync(ct);

            foreach (var item in list)
            {
                if (!item.PlaylistId.HasValue)
                    continue;

                if (playlistNames.TryGetValue(item.PlaylistId.Value, out var name))
                    item.PlaylistName = name;

                var entryMediaType = item.MediaType == "movie" ? MediaTypeValues.Movie : MediaTypeValues.TVShowEpisode;
                var mediaId = item.Entry?.Id;
                if (mediaId.HasValue)
                {
                    var match = playlistEntryIds.FirstOrDefault(pe =>
                        pe.PlaylistId == item.PlaylistId.Value &&
                        pe.MediaType == entryMediaType &&
                        pe.MediaId == mediaId.Value);
                    if (match != null)
                        item.PlaylistEntryId = match.Id;
                }
            }
        }

        /// <summary>
        /// Buffers progress for later processing.
        /// </summary>
        /// <param name="user">The user instance.</param>
        /// <param name="movieId">The movie identifier.</param>
        /// <param name="episodeId">The episode identifier.</param>
        /// <param name="position">The playback position.</param>
        /// <param name="duration">The media duration.</param>
        /// <param name="playlistId">The playlist identifier, when reported from within a playlist playback context.</param>
        /// <param name="ct">A cancellation token.</param>
        public async Task ReportProgressAsync(ApplicationUser user,
                                        long? movieId,
                                        long? episodeId,
                                        TimeSpan position,
                                        TimeSpan duration,
                                        long? playlistId = null,
                                        CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(user?.Id)) return;
            // Optional: Schon jetzt <5s herausfiltern, um Puffer zu entlasten
            if (position < MinStart) return;

            await ValidatePlaylistOwnershipAsync(user!.Id!, playlistId, ct);

            _buffer.EnqueueOrUpdate(user.Id!, movieId, episodeId, position, duration, playlistId);
        }

        /// <summary>
        /// Processes a buffered progress entry and updates storage.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="movieId">The movie identifier.</param>
        /// <param name="episodeId">The episode identifier.</param>
        /// <param name="position">The playback position.</param>
        /// <param name="duration">The media duration.</param>
        /// <param name="playlistId">The playlist identifier, when reported from within a playlist playback context.</param>
        /// <param name="ct">A cancellation token.</param>
        public async Task ProcessBufferedEntryAsync(string userId,
                                                   long? movieId,
                                                   long? episodeId,
                                                   TimeSpan position,
                                                   TimeSpan duration,
                                                   long? playlistId = null,
                                                   CancellationToken ct = default)
        {
            await ValidatePlaylistOwnershipAsync(userId, playlistId, ct);

            var endThreshold = await _programSettings.GetContinueWatchingEndThresholdAsync(ct);

            // Beendet?
            if (duration - position <= endThreshold)
            {
                await _watchedStatusService.MarkWatchedAsync(
                    userId,
                    movieId,
                    episodeId,
                    _timeProvider.GetUtcNow().UtcDateTime,
                    ct);

                // Gesehen-Markierung ist playlist-uebergreifend: ALLE Varianten dieses Videos werden
                // entfernt, unabhaengig von ihrer PlaylistId.
                var existingEntries = await _db.ContinueWatchingEntries
                    .Where(x => x.UserId == userId && x.MovieId == movieId && x.TVShowEpisodeId == episodeId)
                    .ToListAsync(ct);

                if (existingEntries.Count > 0)
                {
                    _db.ContinueWatchingEntries.RemoveRange(existingEntries);
                    await _db.SaveChangesAsync(ct);
                }

                if (movieId.HasValue)
                {
                    var nextMovie = await GetNextMovieAsync(movieId.Value, ct);
                    if (nextMovie != null)
                        await UpsertAsync(userId, nextMovieId: nextMovie.Id, nextEpisodeId: null, playlistId, TimeSpan.Zero, duration: null, ct);
                }
                else if (episodeId.HasValue)
                {
                    var nextEpisode = await GetNextEpisodeAsync(episodeId.Value, ct);
                    if (nextEpisode != null)
                        await UpsertAsync(userId, nextMovieId: null, nextEpisodeId: nextEpisode.Id, playlistId, TimeSpan.Zero, duration: null, ct);
                }

                // Wenn wir wirklich etwas entfernt haben, aber kein nächstes Medium gefunden wurde,
                // muss trotzdem ein Update raus.
                if (existingEntries.Count > 0)
                {
                    var hasNext = movieId.HasValue
                        ? (await GetNextMovieAsync(movieId.Value, ct)) != null
                        : (episodeId.HasValue && (await GetNextEpisodeAsync(episodeId.Value, ct)) != null);

                    if (!hasNext)
                        await _notificationService.NotifyContinueWatchingUpdatedAsync(userId, ct);
                }
                return;
            }

            await UpsertAsync(userId, movieId, episodeId, playlistId, position, duration, ct);
        }

        /// <summary>
        /// Removes a continue-watching entry for the authenticated user.
        /// </summary>
        /// <param name="userId">The authenticated user identifier.</param>
        /// <param name="movieId">The movie identifier, if the entry is a movie.</param>
        /// <param name="episodeId">The episode identifier, if the entry is an episode.</param>
        /// <param name="playlistId">The playlist identifier of the entry to remove, or <c>null</c> for a non-playlist entry.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns><c>true</c> when an entry was removed; otherwise <c>false</c>.</returns>
        public async Task<bool> HideAsync(string userId, long? movieId, long? episodeId, long? playlistId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var entry = await _db.ContinueWatchingEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == movieId && x.TVShowEpisodeId == episodeId && x.PlaylistId == playlistId, ct);

            if (entry is null)
                return false;

            _db.ContinueWatchingEntries.Remove(entry);
            await _db.SaveChangesAsync(ct);
            await _notificationService.NotifyContinueWatchingUpdatedAsync(userId, ct);
            return true;
        }

        /// <summary>
        /// Replaces a continue-watching entry with the next media item while preserving its list position.
        /// </summary>
        /// <param name="userId">The authenticated user identifier.</param>
        /// <param name="movieId">The movie identifier, if the entry is a movie.</param>
        /// <param name="episodeId">The episode identifier, if the entry is an episode.</param>
        /// <param name="playlistId">The playlist identifier of the entry to skip, or <c>null</c> for a non-playlist entry.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The skip result.</returns>
        public async Task<SkipResult> SkipAsync(string userId, long? movieId, long? episodeId, long? playlistId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return SkipResult.NotFound;

            var entry = await _db.ContinueWatchingEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == movieId && x.TVShowEpisodeId == episodeId && x.PlaylistId == playlistId, ct);

            if (entry is null)
                return SkipResult.NotFound;

            var preservedListOrder = entry.ListOrder;
            var nextMovie = movieId.HasValue ? await GetNextMovieAsync(movieId.Value, ct) : null;
            var nextEpisode = episodeId.HasValue ? await GetNextEpisodeAsync(episodeId.Value, ct) : null;

            _db.ContinueWatchingEntries.Remove(entry);

            if (nextMovie is null && nextEpisode is null)
            {
                await _db.SaveChangesAsync(ct);
                await _notificationService.NotifyContinueWatchingUpdatedAsync(userId, ct);
                return SkipResult.RemovedWithoutNext;
            }

            var nextMovieId = nextMovie?.Id;
            var nextEpisodeId = nextEpisode?.Id;

            await RemoveExtsingMovieCollectionEntry(userId, nextMovieId, playlistId, ct);
            await RemoveExistingTVShowEntry(userId, nextEpisodeId, playlistId, ct);

            var replacement = await _db.ContinueWatchingEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == nextMovieId && x.TVShowEpisodeId == nextEpisodeId && x.PlaylistId == playlistId, ct);

            if (replacement is null)
            {
                _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
                {
                    UserId = userId,
                    MovieId = nextMovieId,
                    TVShowEpisodeId = nextEpisodeId,
                    PlaylistId = playlistId,
                    Position = TimeSpan.Zero,
                    Duration = null,
                    UpdatedAt = DateTime.UtcNow,
                    ListOrder = preservedListOrder
                });
            }
            else
            {
                replacement.Position = TimeSpan.Zero;
                replacement.Duration = null;
                replacement.UpdatedAt = DateTime.UtcNow;
                replacement.ListOrder = preservedListOrder;
            }

            await _db.SaveChangesAsync(ct);
            await _notificationService.NotifyContinueWatchingUpdatedAsync(userId, ct);
            return SkipResult.Replaced;
        }

        private async Task UpsertAsync(string userId, long? nextMovieId, long? nextEpisodeId, long? playlistId, TimeSpan position, TimeSpan? duration, CancellationToken ct)
        {
            var entry = await _db.ContinueWatchingEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == nextMovieId && x.TVShowEpisodeId == nextEpisodeId && x.PlaylistId == playlistId, ct);

            static bool DurationChanged(TimeSpan? a, TimeSpan? b)
            {
                if (a is null && b is null) return false;
                if (a is null || b is null) return true;
                return Math.Abs((a.Value - b.Value).TotalSeconds) >= 1;
            }

            static bool PositionChanged(TimeSpan a, TimeSpan b)
                => Math.Abs((a - b).TotalSeconds) >= 1;

            var listChanged = false;

            // Nur wenn ein NEUER Eintrag erzeugt wird: vorhandene Einträge derselben Filmsammlung / Serie entfernen
            if (entry == null)
            {
                await RemoveExtsingMovieCollectionEntry(userId, nextMovieId, playlistId, ct);
                await RemoveExistingTVShowEntry(userId, nextEpisodeId, playlistId, ct);

                // Wenn die obigen Methoden Entries entfernen, ändert sich die Liste auch ohne neuen Eintrag.
                // (ChangeTracker enthält dann Deletes)
                if (_db.ChangeTracker.Entries<ContinueWatchingEntry>().Any(e => e.State == EntityState.Deleted))
                    listChanged = true;

                entry = new ContinueWatchingEntry
                {
                    UserId = userId,
                    MovieId = nextMovieId,
                    TVShowEpisodeId = nextEpisodeId,
                    PlaylistId = playlistId,
                    Position = position,
                    Duration = duration,
                    UpdatedAt = DateTime.UtcNow,
                    ListOrder = DateTime.UtcNow.Ticks
                };
                _db.ContinueWatchingEntries.Add(entry);
                listChanged = true;
            }
            else
            {
                // Nur updaten, wenn sich wirklich etwas geändert hat.
                // Sonst würde UpdatedAt die Sortierung ändern und unnötige Notifications auslösen.
                if (PositionChanged(entry.Position, position) || DurationChanged(entry.Duration, duration))
                {
                    entry.Position = position;
                    entry.Duration = duration;
                    entry.UpdatedAt = DateTime.UtcNow;
                    entry.ListOrder = DateTime.UtcNow.Ticks;
                    listChanged = true;
                }
            }

            await _db.SaveChangesAsync(ct);

            if (!listChanged)
                return;

            // Sende SignalR-Update an User nur wenn sich wirklich etwas geändert hat
            await _notificationService.NotifyContinueWatchingUpdatedAsync(userId, ct);
        }

        private async Task RemoveExistingTVShowEntry(string userId, long? nextEpisodeId, long? playlistId, CancellationToken ct)
        {
            if (!nextEpisodeId.HasValue) return;
            // Serien-ID über Episode -> Season -> Show ermitteln
            var showId = await (
                from e in _db.TVShowEpisodes
                join s in _db.TVShowSeasons on e.TVShowSeasonId equals s.Id
                where e.Id == nextEpisodeId.Value
                select s.TVShowId
            ).FirstOrDefaultAsync(ct);

            if (showId != 0)
            {
                // Alle anderen Episoden-Einträge derselben Serie MIT DERSELBEN PlaylistId entfernen
                var obsoleteEpisodeEntries = await (
                    from cw in _db.ContinueWatchingEntries
                    join e in _db.TVShowEpisodes on cw.TVShowEpisodeId equals e.Id
                    join s in _db.TVShowSeasons on e.TVShowSeasonId equals s.Id
                    where cw.UserId == userId
                          && cw.TVShowEpisodeId != null
                          && cw.TVShowEpisodeId != nextEpisodeId.Value
                          && cw.PlaylistId == playlistId
                          && s.TVShowId == showId
                    select cw
                ).ToListAsync(ct);

                if (obsoleteEpisodeEntries.Count > 0)
                    _db.ContinueWatchingEntries.RemoveRange(obsoleteEpisodeEntries);
            }
        }

        private async Task RemoveExtsingMovieCollectionEntry(string userId, long? nextMovieId, long? playlistId, CancellationToken ct)
        {
            if (!nextMovieId.HasValue)
                return;

            // Filmsammlung ermitteln
            var collectionId = await _db.Movies
                .Where(m => m.Id == nextMovieId.Value)
                .Select(m => m.MovieCollectionId)
                .FirstOrDefaultAsync(ct);

            if (collectionId.HasValue)
            {
                // Alle anderen ContinueWatching-Einträge des Users aus derselben Sammlung MIT DERSELBEN PlaylistId entfernen
                var obsoleteMovieEntries = await (
                    from cw in _db.ContinueWatchingEntries
                    join m in _db.Movies on cw.MovieId equals m.Id
                    where cw.UserId == userId
                          && cw.MovieId != null
                          && cw.MovieId != nextMovieId.Value
                          && cw.PlaylistId == playlistId
                          && m.MovieCollectionId == collectionId.Value
                    select cw
                ).ToListAsync(ct);

                if (obsoleteMovieEntries.Count > 0)
                    _db.ContinueWatchingEntries.RemoveRange(obsoleteMovieEntries);
            }
        }

        /// <summary>
        /// Validates that <paramref name="userId"/> owns the playlist identified by <paramref name="playlistId"/>.
        /// Does nothing when <paramref name="playlistId"/> is <c>null</c>.
        /// </summary>
        /// <param name="userId">The authenticated user identifier.</param>
        /// <param name="playlistId">The playlist identifier to validate, or <c>null</c> for a non-playlist entry.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <exception cref="KeyNotFoundException">The playlist does not exist.</exception>
        /// <exception cref="PlaylistAccessDeniedException">The user does not own the playlist.</exception>
        public async Task ValidatePlaylistOwnershipAsync(string userId, long? playlistId, CancellationToken ct = default)
        {
            if (!playlistId.HasValue)
                return;

            var playlist = await _playlistService.GetPlaylistAsync(playlistId.Value, userId, ct);
            if (playlist is null)
                throw new KeyNotFoundException("Die angegebene Playlist wurde nicht gefunden.");
        }

        private async Task<string?> GetUserIdAsync(ClaimsPrincipal principal, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(principal);
            return user?.Id;
        }

        private async Task<Movie?> GetNextMovieAsync(long currentMovieId, CancellationToken ct)
        {
            var current = await _db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == currentMovieId, ct);
            if (current == null || current.MovieCollectionId == null) return null;

            var list = await _db.Movies.AsNoTracking()
                .Where(m => m.MovieCollectionId == current.MovieCollectionId)
                .OrderBy(m => m.ReleaseDate == null)
                .ThenBy(m => m.ReleaseDate)
                .ThenBy(m => m.PremieredAt == null)
                .ThenBy(m => m.PremieredAt)
                .ThenBy(m => m.Name)
                .Select(m => m.Id)
                .ToListAsync(ct);

            var idx = list.FindIndex(id => id == currentMovieId);
            if (idx >= 0 && idx + 1 < list.Count) return await _db.Movies.FindAsync(new object[] { list[idx + 1] }, ct);
            return null;
        }

        private async Task<TVShowEpisode?> GetNextEpisodeAsync(long currentEpisodeId, CancellationToken ct)
        {
            var current = await _db.TVShowEpisodes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == currentEpisodeId, ct);
            if (current == null) return null;

            var season = await _db.TVShowSeasons.FirstOrDefaultAsync(s => s.Id == current.TVShowSeasonId, ct);
            if (season is null) return null;

            var next = await _db.TVShowEpisodes.AsNoTracking()
                .Where(e => e.TVShowSeasonId == current.TVShowSeasonId && e.Number > current.Number)
                .OrderBy(e => e.Number)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(ct);

            if (next == 0)
            {
                var nextSeason = (await _db.TVShowSeasons.Where(s => s.TVShowId == season.TVShowId)
                    .OrderBy(s => s.Name)
                    .ToListAsync(ct))
                    .SkipWhile(s => s.Id != season.Id)
                    .SkipWhile(s => s.Id == season.Id)
                    .FirstOrDefault();
                if (nextSeason is null) return null;
                next = await _db.TVShowEpisodes.AsNoTracking()
                    .Where(e => e.TVShowSeasonId == nextSeason.Id)
                    .OrderBy(e => e.Number)
                    .Select(e => e.Id)
                    .FirstOrDefaultAsync(ct);
            }

            return next == 0 ? null : await _db.TVShowEpisodes.FindAsync(new object[] { next }, ct);
        }
    }
}
