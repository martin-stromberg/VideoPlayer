using VideoWebPlayer.Services;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using System.Threading;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

/// <summary>
/// Manages recent entries for media items.
/// </summary>
public class RecentEntryService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthService authService;
    private readonly IUnlockedMediaService _unlockedMediaService;
    private const int MaxEntries = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecentEntryService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="authService">Authentication service.</param>
    public RecentEntryService(ApplicationDbContext db, IAuthService authService, IUnlockedMediaService unlockedMediaService)
    {
        _db = db;
        this.authService = authService;
        _unlockedMediaService = unlockedMediaService;
    }

    private async Task ClearCorruptEntriesAsync()
    {
       foreach (var entry in await _db.RecentEntries.ToListAsync())
       {
            switch(entry.Type)
            {
                case RecentEntryType.Movie:
                    if (entry.MovieId == null || entry.MovieId == 0)
                        _db.RecentEntries.Remove(entry);
                    break;
                case RecentEntryType.MovieCollection:
                    if (entry.MovieCollectionId == null || entry.MovieCollectionId == 0)
                        _db.RecentEntries.Remove(entry);
                    break;
                case RecentEntryType.TVShow:
                    if (entry.TVShowId == null || entry.TVShowId == 0)
                        _db.RecentEntries.Remove(entry);
                    break;
                case RecentEntryType.TVShowSeason:
                    if (entry.TVShowSeasonId == null || entry.TVShowSeasonId == 0)
                        _db.RecentEntries.Remove(entry);
                    break;
                case RecentEntryType.TVShowEpisode:
                    if (entry.TVShowEpisodeId == null || entry.TVShowEpisodeId == 0)
                        _db.RecentEntries.Remove(entry);
                    break;
            }
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Adds a recent entry for a movie.
    /// </summary>
    /// <param name="movie">The movie entry.</param>
    public async Task AddMovieAsync(Movie movie)
    {
        await ClearCorruptEntriesAsync();

        // Prüfe, ob die MovieCollection schon gelistet ist
        if (movie.MovieCollectionId.HasValue &&
            await _db.RecentEntries.AnyAsync(e => e.MovieCollectionId == movie.MovieCollectionId))
            return;

        // Entferne ggf. vorhandene Einträge für denselben Film
        var existing = await _db.RecentEntries.Where(e => e.MovieId == movie.Id).ToListAsync();
        _db.RecentEntries.RemoveRange(existing);

        // Füge neuen Eintrag hinzu
        _db.RecentEntries.Add(new RecentEntry
        {
            MediaSourceId = movie.MediaSourceId,
            PublishedAt = movie.PremieredAt ?? movie.ReleaseDate ?? movie.CreatedAt,
            Type = RecentEntryType.Movie,
            MovieId = movie.Id,
            MovieCollectionId = movie.MovieCollectionId
        });

        await TrimEntriesAsync(movie.MediaSourceId);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Adds a recent entry for a movie collection.
    /// </summary>
    /// <param name="collection">The movie collection.</param>
    public async Task AddMovieCollectionAsync(MovieCollection collection)
    {
        await ClearCorruptEntriesAsync();
        // Entferne alle Filme dieser Collection aus RecentEntries
        var movies = await _db.Movies.Where(m => m.MovieCollectionId == collection.Id).Select(m => m.Id).ToListAsync();
        var toRemove = await _db.RecentEntries.Where(e => movies.Contains(e.MovieId ?? 0)).ToListAsync();
        _db.RecentEntries.RemoveRange(toRemove);

        // Füge Collection hinzu, falls nicht vorhanden
        if (!await _db.RecentEntries.AnyAsync(e => e.MovieCollectionId == collection.Id))
        {
            _db.RecentEntries.Add(new RecentEntry
            {
                MediaSourceId = collection.MediaSourceId,
                PublishedAt = collection.PremieredAt ?? collection.ReleaseDate ?? collection.CreatedAt,
                Type = RecentEntryType.MovieCollection,
                MovieCollectionId = collection.Id
            });
        }

        await TrimEntriesAsync(collection.MediaSourceId);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Adds a recent entry for a TV show.
    /// </summary>
    /// <param name="show">The TV show.</param>
    public async Task AddTVShowAsync(TVShow show)
    {
        await ClearCorruptEntriesAsync();
        // Entferne alle Staffeln und Episoden dieser Show
        var seasons = await _db.TVShowSeasons.Where(s => s.TVShowId == show.Id).Select(s => s.Id).ToListAsync();
        var episodes = await _db.TVShowEpisodes.Where(e => seasons.Contains(e.TVShowSeasonId)).Select(e => e.Id).ToListAsync();
        var toRemove = await _db.RecentEntries.Where(e =>
            e.TVShowId == show.Id ||
            (e.TVShowSeasonId.HasValue && seasons.Contains(e.TVShowSeasonId.Value)) ||
            (e.TVShowEpisodeId.HasValue && episodes.Contains(e.TVShowEpisodeId.Value))
        ).ToListAsync();
        _db.RecentEntries.RemoveRange(toRemove);

        // Füge Show hinzu, falls nicht vorhanden
        if (!await _db.RecentEntries.AnyAsync(e => e.TVShowId == show.Id))
        {
            _db.RecentEntries.Add(new RecentEntry
            {
                MediaSourceId = show.MediaSourceId,
                PublishedAt = show.PremieredAt ?? show.ReleaseDate ?? show.CreatedAt,
                Type = RecentEntryType.TVShow,
                TVShowId = show.Id
            });
            await _db.SaveChangesAsync();
        }
        await TrimEntriesAsync(show.MediaSourceId);
    }

    /// <summary>
    /// Adds a recent entry for a TV show season.
    /// </summary>
    /// <param name="season">The TV show season.</param>
    public async Task AddTVShowSeasonAsync(TVShowSeason season)
    {
        await ClearCorruptEntriesAsync();
        // Prüfe, ob die Show schon gelistet ist
        if (await _db.RecentEntries.AnyAsync(e => e.TVShowId == season.TVShowId && e.Type == RecentEntryType.TVShow))
            return;

        // Hole alle Staffeln der Show
       var allSeasons = await _db.TVShowSeasons
            .Where(s => s.TVShowId == season.TVShowId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        if (!int.TryParse(season.Name.Split(' ').Last(), out var thisSeasonNumber))
            return;

        // Finde alle Einträge zu Staffeln/Episoden dieser Show
        var entries = await _db.RecentEntries
            .Where(e => e.TVShowId == season.TVShowId)
            .ToListAsync();

        bool shouldAdd = false;

        foreach (var entry in entries)
        {
            if (entry.Type == RecentEntryType.TVShowSeason)
            {
                var entrySeason = await _db.TVShowSeasons.FindAsync(entry.TVShowSeasonId);
                if (entrySeason != null)
                {
                    if (!int.TryParse(season.Name.Split(' ').Last(), out var entrySeasonNumber))
                        entrySeasonNumber = int.MaxValue;

                    if (entrySeasonNumber > thisSeasonNumber)
                    {
                        // Spätere Staffel vorhanden, neue Staffel hinzufügen
                        shouldAdd = true;
                    }
                    else if (entrySeasonNumber < thisSeasonNumber)
                    {
                        // Frühere Staffel vorhanden, neue Staffel NICHT hinzufügen
                        return;
                    }
                    else
                    {
                        // Gleiche Staffel, NICHT hinzufügen
                        return;
                    }
                }
            }
            else if (entry.Type == RecentEntryType.TVShowEpisode)
            {
                var entryEpisode = await _db.TVShowEpisodes.FindAsync(entry.TVShowEpisodeId);
                if (entryEpisode != null)
                {
                    var entrySeason = await _db.TVShowSeasons.FindAsync(entryEpisode.TVShowSeasonId);
                    if (entrySeason != null)
                    {
                        if (!int.TryParse(season.Name.Split(' ').Last(), out var entrySeasonNumber))
                            entrySeasonNumber = int.MaxValue;
                        if (entrySeasonNumber > thisSeasonNumber)
                        {
                            // Episode einer späteren Staffel vorhanden, neue Staffel hinzufügen
                            shouldAdd = true;
                        }
                        else if (entrySeasonNumber < thisSeasonNumber)
                        {
                            // Episode einer früheren Staffel vorhanden, neue Staffel NICHT hinzufügen
                            return;
                        }
                        else
                        {
                            // Episode der gleichen Staffel vorhanden, neue Staffel hinzufügen
                            shouldAdd = true;
                        }
                    }
                }
            }
        }

        // Entferne alle Einträge zu späteren Staffeln und deren Episoden
        if (shouldAdd)
        {
            var laterSeasonIds = allSeasons.Where(s => s.Name.EndsWith(thisSeasonNumber.ToString().PadLeft(2, '0'))).Select(s => s.Id).ToList();
            var toRemove = await _db.RecentEntries
                .Where(e =>
                    (e.TVShowSeasonId.HasValue && laterSeasonIds.Contains(e.TVShowSeasonId.Value)) ||
                    (e.TVShowEpisodeId.HasValue && laterSeasonIds.Contains(
                        (_db.TVShowEpisodes.Where(ep => ep.Id == e.TVShowEpisodeId).Select(ep => ep.TVShowSeasonId).FirstOrDefault())
                    ))
                ).ToListAsync();

            _db.RecentEntries.RemoveRange(toRemove);

            // Füge Staffel hinzu, falls nicht vorhanden
            if (!await _db.RecentEntries.AnyAsync(e => e.TVShowSeasonId == season.Id))
            {
                _db.RecentEntries.Add(new RecentEntry
                {
                    MediaSourceId = season.MediaSourceId,
                    PublishedAt = season.PremieredAt ?? season.ReleaseDate ?? season.CreatedAt,
                    Type = RecentEntryType.TVShowSeason,
                    TVShowId = season.TVShowId,
                    TVShowSeasonId = season.Id
                });
                await _db.SaveChangesAsync();
            }
            await TrimEntriesAsync(season.MediaSourceId);
        }
    }

    /// <summary>
    /// Adds a recent entry for a TV show episode.
    /// </summary>
    /// <param name="episode">The TV show episode.</param>
    public async Task AddTVShowEpisodeAsync(TVShowEpisode episode)
    {
        await ClearCorruptEntriesAsync();
        var season = await _db.TVShowSeasons
            .FirstOrDefaultAsync(s => s.Id == episode.TVShowSeasonId);
        if (season is null)
            return;
        // Prüfe, ob die Show oder Staffel schon gelistet ist
        if (await _db.RecentEntries.AnyAsync(e => e.TVShowId == season.TVShowId) ||
            await _db.RecentEntries.AnyAsync(e => e.TVShowSeasonId == episode.TVShowSeasonId))
            return;

        // Prüfe, ob ein Eintrag mit früherem Veröffentlichungsdatum existiert
        var existing = await _db.RecentEntries
            .Where(e => e.TVShowEpisodeId == episode.Id)
            .FirstOrDefaultAsync();

        var publishedAt = episode.PremieredAt ?? episode.ReleaseDate ?? episode.CreatedAt;

        if (existing != null && existing.PublishedAt >= publishedAt)
            return;

        // Entferne spätere Einträge
        var laterEntries = await _db.RecentEntries
            .Where(e => e.TVShowEpisodeId == episode.Id && e.PublishedAt < publishedAt)
            .ToListAsync();
        _db.RecentEntries.RemoveRange(laterEntries);

        // Füge Episode hinzu
        _db.RecentEntries.Add(new RecentEntry
        {
            MediaSourceId = episode.MediaSourceId,
            PublishedAt = publishedAt,
            Type = RecentEntryType.TVShowEpisode,
            TVShowId = season.TVShowId,
            TVShowSeasonId = episode.TVShowSeasonId,
            TVShowEpisodeId = episode.Id
        });
        await _db.SaveChangesAsync();
        await TrimEntriesAsync(episode.MediaSourceId);        
    }

    private async Task TrimEntriesAsync(long mediaSourceId)
    {
        var entries = await _db.RecentEntries
            .Where(e => e.MediaSourceId == mediaSourceId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        if (entries.Count > MaxEntries)
        {
            var toRemove = entries.Skip(MaxEntries).ToList();
            _db.RecentEntries.RemoveRange(toRemove);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets the recent entries for the current user.
    /// </summary>
    /// <returns>The recent entries.</returns>
    public async Task<List<RecentEntry>> GetRecentEntriesAsync()
    {
        var currentUser = authService.CurrentUser;
        var mediaSourceIds = await _db.MediaSourceUsers.Where(msu => msu.UserId == currentUser.Id).Select(msu => msu.MediaSourceId).ToArrayAsync();
        var unlockedSourceIds = await _unlockedMediaService.GetUnlockedSourceIdsForUserAsync(currentUser.Id);
        var unlockedMovieCollectionIds = await _unlockedMediaService.GetUnlockedMovieCollectionIdsForUserAsync(currentUser.Id);
        var unlockedTVShowIds = await _unlockedMediaService.GetUnlockedTVShowIdsForUserAsync(currentUser.Id);
        var allowedSourceIds = mediaSourceIds.Union(unlockedSourceIds).ToArray();

        return await _db.RecentEntries
            .Where(m => allowedSourceIds.Contains(m.MediaSourceId) ||
                (m.MovieCollectionId != null && unlockedMovieCollectionIds.Contains(m.MovieCollectionId.Value)) ||
                (m.TVShowId != null && unlockedTVShowIds.Contains(m.TVShowId.Value)) ||
                (m.TVShowSeasonId != null && _db.TVShowSeasons.Any(s => s.Id == m.TVShowSeasonId.Value && unlockedTVShowIds.Contains(s.TVShowId))) ||
                (m.TVShowEpisodeId != null && _db.TVShowEpisodes.Any(e => e.Id == m.TVShowEpisodeId.Value && _db.TVShowSeasons.Any(s => s.Id == e.TVShowSeasonId && unlockedTVShowIds.Contains(s.TVShowId)))))
            .OrderByDescending(e => e.CreatedAt)
            .Take(MaxEntries)
            .ToListAsync();
    }
}