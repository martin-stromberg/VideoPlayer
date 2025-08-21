using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services
{
    public class ContinueWatchingService
    {
        private static readonly TimeSpan MinStart = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan EndThreshold = TimeSpan.FromSeconds(30);

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ContinueWatchingService> _logger;
        private readonly ContinueWatchingBuffer _buffer;

        public ContinueWatchingService(ApplicationDbContext db,
                                       UserManager<ApplicationUser> userManager,
                                       ILogger<ContinueWatchingService> logger,
                                       ContinueWatchingBuffer buffer)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _buffer = buffer;
        }

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

        public async Task<List<ContinueWatchingDto>> GetListAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            var userId = await GetUserIdAsync(user, ct);
            if (userId == null) return new();

            var list =(await _db.ContinueWatchingEntries
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

        // NEU: nur puffern (überschreibt vorhandenen Zwischenspeicher-Eintrag für denselben Key)
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

        // Vom Worker aufgerufen: verarbeitet einen Snapshot wie bisher
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

            if (entry == null)
            {
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

            var next = await _db.TVShowEpisodes.AsNoTracking()
                .Where(e => e.TVShowSeasonId == current.TVShowSeasonId && e.Id > currentEpisodeId)
                .OrderBy(e => e.Id)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(ct);

            return next == 0 ? null : await _db.TVShowEpisodes.FindAsync(new object[] { next }, ct);
        }
    }

    
}