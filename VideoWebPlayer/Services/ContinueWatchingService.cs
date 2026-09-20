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
                    Id = x.Id,
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

            await EnrichPlaylistInfoAsync(list, userId, ct);

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
        /// <remarks>
        /// Entwicklungsschritt 11: only playlists <paramref name="userId"/> may currently read (owned by the
        /// user, or marked public) contribute their name and entry id. A continue-watching entry that still
        /// points at a playlist the user can no longer see (defensive - clearing the public flag already
        /// detaches other users' entries, see <see cref="DetachOtherUsersFromPlaylistAsync"/>) therefore
        /// never discloses that playlist's name or contents.
        /// </remarks>
        /// <param name="list">The continue-watching DTOs to enrich, in place.</param>
        /// <param name="userId">The id of the user the list is built for.</param>
        /// <param name="ct">A cancellation token.</param>
        private async Task EnrichPlaylistInfoAsync(List<ContinueWatchingDto> list, string userId, CancellationToken ct)
        {
            var playlistIds = list.Where(x => x.PlaylistId.HasValue).Select(x => x.PlaylistId!.Value).Distinct().ToList();
            if (playlistIds.Count == 0)
                return;

            var playlistNames = await _db.Playlists
                .AsNoTracking()
                .Where(p => playlistIds.Contains(p.Id) && (p.UserId == userId || p.IsPublic))
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

            var readablePlaylistIds = playlistNames.Keys.ToList();
            var playlistEntryIds = await _db.PlaylistEntries
                .AsNoTracking()
                .Where(pe => readablePlaylistIds.Contains(pe.PlaylistId))
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

            await ValidatePlaylistAccessAsync(user!.Id!, playlistId, ct);

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
            await ValidatePlaylistAccessAsync(userId, playlistId, ct);

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
        /// Validates that <paramref name="userId"/> may read the playlist identified by
        /// <paramref name="playlistId"/> (Entwicklungsschritt 11: the owner, or any user while the playlist is
        /// marked public) - the precondition for reporting playback progress bound to that playlist. The
        /// resulting continue-watching entry always belongs to <paramref name="userId"/> itself, so a viewer of
        /// a public playlist keeps their own progress without touching the owner's or anybody else's.
        /// Does nothing when <paramref name="playlistId"/> is <c>null</c>.
        /// </summary>
        /// <param name="userId">The authenticated user identifier.</param>
        /// <param name="playlistId">The playlist identifier to validate, or <c>null</c> for a non-playlist entry.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <exception cref="KeyNotFoundException">The playlist does not exist.</exception>
        /// <exception cref="PlaylistAccessDeniedException">The playlist is private and owned by somebody else.</exception>
        public async Task ValidatePlaylistAccessAsync(string userId, long? playlistId, CancellationToken ct = default)
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

        /// <summary>
        /// Resolves unique-index conflicts that would otherwise occur once <paramref name="playlistId"/> is
        /// deleted and the database's <c>ON DELETE SET NULL</c> foreign-key action sets
        /// <see cref="ContinueWatchingEntry.PlaylistId"/> to <c>null</c> for its bound entries: for every
        /// entry currently bound to <paramref name="playlistId"/> - of <b>every</b> user, not just the owner
        /// (Entwicklungsschritt 11: viewers of a public playlist carry playlist-bound entries too) - removes it
        /// if a playlist-less entry for the same user and media already exists (which would otherwise
        /// collide with it once both have <see cref="ContinueWatchingEntry.PlaylistId"/> <c>null</c>);
        /// otherwise leaves it in place, to be set to <c>null</c> by the database once the playlist itself is
        /// deleted. Marks the affected rows for removal on the tracked <see cref="ApplicationDbContext"/>
        /// without calling <see cref="ApplicationDbContext.SaveChangesAsync(CancellationToken)"/> itself; the
        /// caller is expected to do so together with the playlist's own deletion.
        /// </summary>
        /// <param name="playlistId">The id of the playlist about to be deleted.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The ids of the users whose continue-watching list is affected.</returns>
        internal Task<IReadOnlyCollection<string>> ResolvePlaylistDeletionConflictsAsync(long playlistId, CancellationToken ct)
            => ResolveDeletionConflictsAsync(new[] { playlistId }, detachSurvivors: false, excludeUserId: null, ct);

        /// <summary>
        /// Same as <see cref="ResolvePlaylistDeletionConflictsAsync"/>, but for every playlist owned by
        /// <paramref name="ownerUserId"/> at once - used before a user account is deleted (its playlists are
        /// removed by the database's cascading delete, so other users' entries bound to the owner's public
        /// playlists would hit the same UNIQUE-index conflict). Persists the removals itself.
        /// </summary>
        /// <param name="ownerUserId">The id of the user about to be deleted.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The ids of the users whose continue-watching list is affected.</returns>
        public async Task<IReadOnlyCollection<string>> ResolveDeletionConflictsForOwnedPlaylistsAsync(string ownerUserId, CancellationToken ct = default)
        {
            var playlistIds = await _db.Playlists.AsNoTracking()
                .Where(p => p.UserId == ownerUserId)
                .Select(p => p.Id)
                .ToListAsync(ct);

            var affected = await ResolveDeletionConflictsAsync(playlistIds, detachSurvivors: false, excludeUserId: null, ct);
            await _db.SaveChangesAsync(ct);
            return affected;
        }

        /// <summary>
        /// Detaches every user's continue-watching entries except the owner's from
        /// <paramref name="playlistId"/> and persists the result (together with any other pending change on
        /// the shared <see cref="ApplicationDbContext"/>, notably the playlist's own
        /// <see cref="Playlist.IsPublic"/> = <see langword="false"/> - so both happen in one atomic
        /// <c>SaveChangesAsync</c>). Entwicklungsschritt 11 decision for "the public flag was removed": other
        /// users lose access to the playlist at once, so their continue-watching entries must not keep
        /// pointing at it (no deep link into it, no playlist name shown). Each such entry becomes a plain,
        /// playlist-less entry (the viewer keeps their playback position); if the viewer already has a
        /// playlist-less entry for the same media, that existing entry wins and the bound one is removed
        /// (same rule as for playlist deletion, see <see cref="ResolvePlaylistDeletionConflictsAsync"/>).
        /// </summary>
        /// <param name="playlistId">The id of the playlist that stops being public.</param>
        /// <param name="ownerUserId">The id of the owner, whose own entries stay bound to the playlist.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The ids of the users whose continue-watching list changed.</returns>
        internal async Task<IReadOnlyCollection<string>> DetachOtherUsersFromPlaylistAsync(long playlistId, string ownerUserId, CancellationToken ct)
        {
            var affected = await ResolveDeletionConflictsAsync(new[] { playlistId }, detachSurvivors: true, excludeUserId: ownerUserId, ct);
            await _db.SaveChangesAsync(ct);
            return affected;
        }

        /// <summary>
        /// Notifies the given users (over SignalR) that their continue-watching list changed. No-op for an
        /// empty collection.
        /// </summary>
        /// <param name="userIds">The users to notify.</param>
        /// <param name="ct">A cancellation token.</param>
        internal async Task NotifyUsersAsync(IEnumerable<string> userIds, CancellationToken ct)
        {
            foreach (var userId in userIds.Distinct())
                await _notificationService.NotifyContinueWatchingUpdatedAsync(userId, ct);
        }

        /// <summary>
        /// Returns the ids of every user that has a continue-watching entry bound to
        /// <paramref name="playlistId"/> referencing the given media (movies/episodes only; any other media
        /// type yields an empty result), for resolving what a playlist entry removal does to each user.
        /// </summary>
        /// <param name="playlistId">The playlist the entries must be bound to.</param>
        /// <param name="mediaType">The media type of the removed playlist entry.</param>
        /// <param name="mediaId">The media id of the removed playlist entry.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The distinct user ids.</returns>
        internal async Task<IReadOnlyCollection<string>> GetUserIdsWithPlaylistBoundEntryAsync(long playlistId, string mediaType, long mediaId, CancellationToken ct)
        {
            var (movieId, episodeId) = ResolveMovieAndEpisodeIds(mediaType, mediaId);
            if (movieId is null && episodeId is null)
                return Array.Empty<string>();

            return await _db.ContinueWatchingEntries.AsNoTracking()
                .Where(x => x.PlaylistId == playlistId && x.MovieId == movieId && x.TVShowEpisodeId == episodeId)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync(ct);
        }

        private async Task<IReadOnlyCollection<string>> ResolveDeletionConflictsAsync(
            IReadOnlyCollection<long> playlistIds, bool detachSurvivors, string? excludeUserId, CancellationToken ct)
        {
            if (playlistIds.Count == 0)
                return Array.Empty<string>();

            var boundEntries = await _db.ContinueWatchingEntries
                .Where(x => x.PlaylistId != null && playlistIds.Contains(x.PlaylistId.Value) && (excludeUserId == null || x.UserId != excludeUserId))
                .OrderByDescending(x => x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .ToListAsync(ct);

            if (boundEntries.Count == 0)
                return Array.Empty<string>();

            var userIds = boundEntries.Select(x => x.UserId).Distinct().ToList();
            var freeEntries = await _db.ContinueWatchingEntries
                .Where(x => x.PlaylistId == null && userIds.Contains(x.UserId))
                .ToListAsync(ct);

            // The keys (user, movie/episode) that will hold a playlist-less entry after this operation: the
            // already existing ones, plus - as each surviving bound entry is claimed - that entry itself, so
            // two bound entries of one user for the same media (from different playlists being removed at
            // once, e.g. on account deletion) cannot collide with each other either. The most recently
            // updated bound entry wins.
            var occupied = new HashSet<(string UserId, long? MovieId, long? EpisodeId)>(
                freeEntries.Select(f => (f.UserId, f.MovieId, f.TVShowEpisodeId)));

            foreach (var entry in boundEntries)
            {
                var key = (entry.UserId, entry.MovieId, entry.TVShowEpisodeId);
                if (!occupied.Add(key))
                {
                    _db.ContinueWatchingEntries.Remove(entry);
                    continue;
                }

                if (detachSurvivors)
                    entry.PlaylistId = null;
            }

            return userIds;
        }

        /// <summary>
        /// Whether a continue-watching entry bound to <paramref name="playlistId"/> currently references
        /// the given media, for <see cref="PlaylistService.RemoveMediaFromPlaylistAsync"/>'s
        /// removal-confirmation check. Only movies and TV show episodes can ever be referenced (a
        /// continue-watching entry never points at a collection entry such as TVShow/TVShowSeason/
        /// MovieCollection), so any other <paramref name="mediaType"/> trivially returns <see langword="false"/>.
        /// </summary>
        /// <param name="playlistId">The id of the playlist the continue-watching entry must be bound to.</param>
        /// <param name="userId">The id of the owning user.</param>
        /// <param name="mediaType">The media type of the entry being checked (a <c>PlaylistEntry.MediaType</c> value).</param>
        /// <param name="mediaId">The media id of the entry being checked.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns><see langword="true"/> if a matching, playlist-bound continue-watching entry exists.</returns>
        internal async Task<bool> HasPlaylistBoundEntryAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken ct)
        {
            var (movieId, episodeId) = ResolveMovieAndEpisodeIds(mediaType, mediaId);
            if (movieId is null && episodeId is null)
                return false;

            return await _db.ContinueWatchingEntries.AnyAsync(
                x => x.UserId == userId && x.PlaylistId == playlistId && x.MovieId == movieId && x.TVShowEpisodeId == episodeId, ct);
        }

        /// <summary>
        /// Resolves the continue-watching entry (if any) that a playlist entry removal affects, shared by
        /// <see cref="PlaylistService.RemoveMediaFromPlaylistAsync"/> (user-confirmed removal of a single
        /// entry) and <see cref="PlaylistService"/>'s silent orphan cleanup (a title disappeared from the
        /// media library and its playlist entry was removed without user interaction) - both scenarios call
        /// this the same way, since from this service's point of view a "removed playlist entry" looks
        /// identical either way. If a continue-watching entry bound to <paramref name="playlistId"/>
        /// references (<paramref name="removedMediaType"/>, <paramref name="removedMediaId"/>), it is either
        /// replaced with (<paramref name="nextMediaType"/>, <paramref name="nextMediaId"/>) - resetting its
        /// playback position, since the new title was not itself watched yet - when a next title was
        /// resolved, or removed entirely when it was not (<paramref name="nextMediaType"/>/<paramref name="nextMediaId"/>
        /// both <see langword="null"/>). Does nothing if no such continue-watching entry exists.
        /// </summary>
        /// <remarks>
        /// Collision handling when replacing: if a distinct continue-watching entry already exists for
        /// (<paramref name="userId"/>, <paramref name="playlistId"/>, next title) - e.g. because the user
        /// already watched that next title separately within the same playlist context - writing the next
        /// title onto the entry being replaced would violate the unique index on
        /// (UserId, MovieId/TVShowEpisodeId, PlaylistId) (see <c>ContinueWatchingEntryConfiguration</c>).
        /// Mirrors the decision already documented for <see cref="ResolvePlaylistDeletionConflictsAsync"/>:
        /// the colliding entry - which carries real, already-existing progress for that title - is kept, and
        /// the entry being replaced is removed instead of overwriting it.
        /// </remarks>
        /// <param name="playlistId">The id of the playlist the removed entry belonged to.</param>
        /// <param name="userId">The id of the owning user.</param>
        /// <param name="removedMediaType">The media type of the removed playlist entry.</param>
        /// <param name="removedMediaId">The media id of the removed playlist entry.</param>
        /// <param name="nextMediaType">The media type of the next available title to replace with, or <see langword="null"/>.</param>
        /// <param name="nextMediaId">The media id of the next available title to replace with, or <see langword="null"/>.</param>
        /// <param name="ct">A cancellation token.</param>
        internal async Task ResolvePlaylistEntryRemovalAsync(
            long playlistId, string userId, string removedMediaType, long removedMediaId,
            string? nextMediaType, long? nextMediaId, CancellationToken ct)
        {
            var (removedMovieId, removedEpisodeId) = ResolveMovieAndEpisodeIds(removedMediaType, removedMediaId);
            if (removedMovieId is null && removedEpisodeId is null)
                return;

            var entry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(
                x => x.UserId == userId && x.PlaylistId == playlistId && x.MovieId == removedMovieId && x.TVShowEpisodeId == removedEpisodeId, ct);
            if (entry is null)
                return;

            if (nextMediaType is null || nextMediaId is null)
            {
                _db.ContinueWatchingEntries.Remove(entry);
                await _db.SaveChangesAsync(ct);
                await _notificationService.NotifyContinueWatchingUpdatedAsync(userId, ct);
                return;
            }

            var (nextMovieId, nextEpisodeId) = ResolveMovieAndEpisodeIds(nextMediaType, nextMediaId.Value);

            var collidingEntry = await _db.ContinueWatchingEntries.FirstOrDefaultAsync(
                x => x.Id != entry.Id && x.UserId == userId && x.PlaylistId == playlistId && x.MovieId == nextMovieId && x.TVShowEpisodeId == nextEpisodeId, ct);

            if (collidingEntry is not null)
            {
                _db.ContinueWatchingEntries.Remove(entry);
            }
            else
            {
                entry.MovieId = nextMovieId;
                entry.TVShowEpisodeId = nextEpisodeId;
                entry.Position = TimeSpan.Zero;
                entry.Duration = null;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            await _notificationService.NotifyContinueWatchingUpdatedAsync(userId, ct);
        }

        /// <summary>
        /// Resolves a <c>PlaylistEntry.MediaType</c> value together with its media id to the underlying
        /// movie/episode id pair used by <see cref="ContinueWatchingEntry"/>, shared by
        /// <see cref="HasPlaylistBoundEntryAsync"/> and <see cref="ResolvePlaylistEntryRemovalAsync"/>.
        /// </summary>
        /// <param name="mediaType">The media type to resolve.</param>
        /// <param name="mediaId">The media id to resolve.</param>
        /// <param name="MovieId">(Return tuple field.) The movie id, if <paramref name="mediaType"/> is <see cref="MediaTypeValues.Movie"/>; otherwise <see langword="null"/>.</param>
        /// <param name="EpisodeId">(Return tuple field.) The episode id, if <paramref name="mediaType"/> is <see cref="MediaTypeValues.TVShowEpisode"/>; otherwise <see langword="null"/>.</param>
        /// <returns>Both <see langword="null"/> for any other (non-playable, collection) media type.</returns>
        private static (long? MovieId, long? EpisodeId) ResolveMovieAndEpisodeIds(string mediaType, long mediaId)
        {
            if (string.Equals(mediaType, MediaTypeValues.Movie, StringComparison.OrdinalIgnoreCase))
                return (mediaId, null);
            if (string.Equals(mediaType, MediaTypeValues.TVShowEpisode, StringComparison.OrdinalIgnoreCase))
                return (null, mediaId);
            return (null, null);
        }
    }
}
