using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Hubs;

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
        private readonly IHubContext<MediaUpdateHub> _hubContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContinueWatchingService"/> class.
        /// </summary>
        /// <param name="db">Application database context.</param>
        /// <param name="userManager">User manager for identity lookups.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="buffer">In-memory buffer for progress entries.</param>
        /// <param name="hubContext">SignalR hub context for push notifications.</param>
        public ContinueWatchingService(ApplicationDbContext db,
                                       UserManager<ApplicationUser> userManager,
                                       ILogger<ContinueWatchingService> logger,
                                       ContinueWatchingBuffer buffer,
                                       IHubContext<MediaUpdateHub> hubContext)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _buffer = buffer;
            _hubContext = hubContext;
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
                .OrderByDescending(x => x.UpdatedAt)
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
                return;
            }

            await UpsertAsync(userId, movieId, episodeId, position, duration, ct);
        }

        private async Task UpsertAsync(string userId, long? nextMovieId, long? nextEpisodeId, TimeSpan position, TimeSpan? duration, CancellationToken ct)
        {
            var entry = await _db.ContinueWatchingEntries
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MovieId == nextMovieId && x.TVShowEpisodeId == nextEpisodeId, ct);

            // Nur wenn ein NEUER Eintrag erzeugt wird: vorhandene Einträge derselben Filmsammlung / Serie entfernen
            if (entry == null)
            {
                await RemoveExtsingMovieCollectionEntry(userId, nextMovieId, ct);
                await RemoveExistingTVShowEntry(userId, nextEpisodeId, ct);

                entry = new ContinueWatchingEntry
                {
                    UserId = userId,
                    MovieId = nextMovieId,
                    TVShowEpisodeId = nextEpisodeId,
                    Position = position,
                    Duration = duration,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.ContinueWatchingEntries.Add(entry);
            }
            else
            {
                entry.Position = position;
                entry.Duration = duration;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);

            // Sende SignalR-Update an User
            try
            {
                //await _hubContext.Clients.All.SendAsync("ContinueWatchingUpdated", cancellationToken: ct);
                await _hubContext.Clients.User(userId)
                    .SendAsync("ContinueWatchingUpdated", cancellationToken: ct);
                _logger.LogInformation("SignalR: ContinueWatchingUpdated sent to user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR update for ContinueWatchingUpdated");
            }
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
                .Where(e => e.TVShowSeasonId == current.TVShowSeasonId && e.Id != current.Id && e.ReleaseDate >= current.ReleaseDate)
                .OrderBy(e => e.ReleaseDate)
                .ThenBy(e => e.Number)
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
                    .OrderBy(e => e.ReleaseDate)
                    .ThenBy(e => e.Number)
                    .Select(e => e.Id)
                    .FirstOrDefaultAsync(ct);
            }

            return next == 0 ? null : await _db.TVShowEpisodes.FindAsync(new object[] { next }, ct);
        }
    }
}