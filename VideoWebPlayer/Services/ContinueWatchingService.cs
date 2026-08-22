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
        private static readonly TimeSpan EndThreshold = TimeSpan.FromSeconds(30);

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ContinueWatchingService> _logger;
        private readonly ContinueWatchingBuffer _buffer;
        private readonly MediaUpdateNotificationService _notificationService;

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
        public ContinueWatchingService(ApplicationDbContext db,
                                       UserManager<ApplicationUser> userManager,
                                       ILogger<ContinueWatchingService> logger,
                                       ContinueWatchingBuffer buffer,
                                       MediaUpdateNotificationService notificationService)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _buffer = buffer;
            _notificationService = notificationService;
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
                })
                .Select(t =>
                {
                    t.PosterPictureId = t.Entry?.PosterPictureId ?? t.Entry?.FanartPictureId;
                    t.Title = t.Entry?.Name ?? t.Title;
                    return t;
                })
                .ToList();

            return list;
        }

        /// <summary>
        /// Buffers progress for later processing.
        /// </summary>
        /// <param name="user">The user instance.</param>
        /// <param name="movieId">The movie identifier.</param>
        /// <param name="episodeId">The episode identifier.</param>
        /// <param name="position">The playback position.</param>
        /// <param name="duration">The media duration.</param>
        /// <param name="ct">A cancellation token.</param>
        public Task ReportProgressAsync(ApplicationUser user,
                                        long? movieId,
                                        long? episodeId,
                                        TimeSpan position,
                                        TimeSpan duration,
                                        CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(user?.Id)) return Task.CompletedTask;
            // Optional: Schon jetzt <5s herausfiltern, um Puffer zu entlasten
            if (position < MinStart) return Task.CompletedTask;

            _buffer.EnqueueOrUpdate(user.Id!, movieId, episodeId, position, duration);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes a buffered progress entry and updates storage.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="movieId">The movie identifier.</param>
        /// <param name="episodeId">The episode identifier.</param>
        /// <param name="position">The playback position.</param>
        /// <param name="duration">The media duration.</param>
        /// <param name="ct">A cancellation token.</param>
        public async Task ProcessBufferedEntryAsync(string userId,
                                                   long? movieId,
                                                   long? episodeId,
                                                   TimeSpan position,
                                                   TimeSpan duration,
                                                   CancellationToken ct = default)
        {
            // Beendet?
            if (duration - position <= EndThreshold)
            {
                var existing = await _db.ContinueWatchingEntries
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == movieId && x.TVShowEpisodeId == episodeId, ct);

                if (existing != null)
                {
                    _db.ContinueWatchingEntries.Remove(existing);
                    await _db.SaveChangesAsync(ct);
                }

                if (movieId.HasValue)
                {
                    var nextMovie = await GetNextMovieAsync(movieId.Value, ct);
                    if (nextMovie != null)
                        await UpsertAsync(userId, nextMovieId: nextMovie.Id, nextEpisodeId: null, TimeSpan.Zero, duration: null, ct);
                }
                else if (episodeId.HasValue)
                {
                    var nextEpisode = await GetNextEpisodeAsync(episodeId.Value, ct);
                    if (nextEpisode != null)
                        await UpsertAsync(userId, nextMovieId: null, nextEpisodeId: nextEpisode.Id, TimeSpan.Zero, duration: null, ct);
                }

                // Wenn wir wirklich etwas entfernt haben, aber kein nächstes Medium gefunden wurde,
                // muss trotzdem ein Update raus.
                if (existing != null)
                {
                    var hasNext = movieId.HasValue
                        ? (await GetNextMovieAsync(movieId.Value, ct)) != null
                        : (episodeId.HasValue && (await GetNextEpisodeAsync(episodeId.Value, ct)) != null);

                    if (!hasNext)
                        await _notificationService.NotifyContinueWatchingUpdatedAsync(userId, ct);
                }
                return;
            }

            await UpsertAsync(userId, movieId, episodeId, position, duration, ct);
        }

        /// <summary>
        /// Removes a continue-watching entry for the authenticated user.
        /// </summary>
        /// <param name="userId">The authenticated user identifier.</param>
        /// <param name="movieId">The movie identifier, if the entry is a movie.</param>
        /// <param name="episodeId">The episode identifier, if the entry is an episode.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns><c>true</c> when an entry was removed; otherwise <c>false</c>.</returns>
        public async Task<bool> HideAsync(string userId, long? movieId, long? episodeId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var entry = await _db.ContinueWatchingEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == movieId && x.TVShowEpisodeId == episodeId, ct);

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
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The skip result.</returns>
        public async Task<SkipResult> SkipAsync(string userId, long? movieId, long? episodeId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return SkipResult.NotFound;

            var entry = await _db.ContinueWatchingEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == movieId && x.TVShowEpisodeId == episodeId, ct);

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

            await RemoveExtsingMovieCollectionEntry(userId, nextMovieId, ct);
            await RemoveExistingTVShowEntry(userId, nextEpisodeId, ct);

            var replacement = await _db.ContinueWatchingEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == nextMovieId && x.TVShowEpisodeId == nextEpisodeId, ct);

            if (replacement is null)
            {
                _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
                {
                    UserId = userId,
                    MovieId = nextMovieId,
                    TVShowEpisodeId = nextEpisodeId,
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

        private async Task UpsertAsync(string userId, long? nextMovieId, long? nextEpisodeId, TimeSpan position, TimeSpan? duration, CancellationToken ct)
        {
            var entry = await _db.ContinueWatchingEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == nextMovieId && x.TVShowEpisodeId == nextEpisodeId, ct);

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
                await RemoveExtsingMovieCollectionEntry(userId, nextMovieId, ct);
                await RemoveExistingTVShowEntry(userId, nextEpisodeId, ct);

                // Wenn die obigen Methoden Entries entfernen, ändert sich die Liste auch ohne neuen Eintrag.
                // (ChangeTracker enthält dann Deletes)
                if (_db.ChangeTracker.Entries<ContinueWatchingEntry>().Any(e => e.State == EntityState.Deleted))
                    listChanged = true;

                entry = new ContinueWatchingEntry
                {
                    UserId = userId,
                    MovieId = nextMovieId,
                    TVShowEpisodeId = nextEpisodeId,
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

        private async Task RemoveExistingTVShowEntry(string userId, long? nextEpisodeId, CancellationToken ct)
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
                // Alle anderen Episoden-Einträge derselben Serie entfernen
                var obsoleteEpisodeEntries = await (
                    from cw in _db.ContinueWatchingEntries
                    join e in _db.TVShowEpisodes on cw.TVShowEpisodeId equals e.Id
                    join s in _db.TVShowSeasons on e.TVShowSeasonId equals s.Id
                    where cw.UserId == userId
                          && cw.TVShowEpisodeId != null
                          && cw.TVShowEpisodeId != nextEpisodeId.Value
                          && s.TVShowId == showId
                    select cw
                ).ToListAsync(ct);

                if (obsoleteEpisodeEntries.Count > 0)
                    _db.ContinueWatchingEntries.RemoveRange(obsoleteEpisodeEntries);
            }
        }

        private async Task RemoveExtsingMovieCollectionEntry(string userId, long? nextMovieId, CancellationToken ct)
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
                // Alle anderen ContinueWatching-Einträge des Users aus derselben Sammlung entfernen
                var obsoleteMovieEntries = await (
                    from cw in _db.ContinueWatchingEntries
                    join m in _db.Movies on cw.MovieId equals m.Id
                    where cw.UserId == userId
                          && cw.MovieId != null
                          && cw.MovieId != nextMovieId.Value
                          && m.MovieCollectionId == collectionId.Value
                    select cw
                ).ToListAsync(ct);

                if (obsoleteMovieEntries.Count > 0)
                    _db.ContinueWatchingEntries.RemoveRange(obsoleteMovieEntries);
            }
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
