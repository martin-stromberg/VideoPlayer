using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>Builds authorized seasonal cards for both web and mobile clients.</summary>
public sealed class SeasonalHomeService(ApplicationDbContext db, TimeProvider clock, WatchedStatusService watchedStatus)
{
    /// <summary>Loads nonempty active sections without exposing other users' sources.</summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="cancellationToken">Cancels database access.</param>
    /// <returns>Ordered genre sections with exclusive UTC expiry.</returns>
    public async Task<List<DtoSeasonalGenre>> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var now = clock.GetUtcNow().UtcDateTime;
        var sources = await db.MediaSourceUsers.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => x.MediaSourceId).ToArrayAsync(cancellationToken);
        var genres = await db.Genres.AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var result = new List<DtoSeasonalGenre>();
        foreach (var genre in genres)
        {
            var expiry = SeasonalGenreWindow.GetExpiry(genre.StartDate, genre.EndDate, now);
            if (expiry is null)
                continue;
            var movies = await db.Movies.AsNoTracking()
                .Where(m => sources.Contains(m.MediaSourceId) && db.MovieGenres.Any(g => g.GenreId == genre.Id && g.MovieId == m.Id))
                .Include(m => m.MovieCollection).ToListAsync(cancellationToken);
            var collectionIds = movies.Where(m => m.MovieCollectionId.HasValue).Select(m => m.MovieCollectionId!.Value).Distinct().ToArray();
            var collections = await db.MovieCollections.AsNoTracking()
                .Where(c => collectionIds.Contains(c.Id) && sources.Contains(c.MediaSourceId))
                .Include(c => c.Movies.Where(m => sources.Contains(m.MediaSourceId))).ToListAsync(cancellationToken);
            var entries = new List<MediaBaseEntry>();
            entries.AddRange(collections.Select(c => c.Movies.Count == 1 ? (MediaBaseEntry)c.Movies.First() : c));
            entries.AddRange(movies.Where(m => m.MovieCollectionId is null));
            entries.AddRange(await db.TVShows.AsNoTracking()
                .Where(s => sources.Contains(s.MediaSourceId) && db.TVShowGenres.Any(g => g.GenreId == genre.Id && g.TVShowId == s.Id))
                .ToListAsync(cancellationToken));
            if (entries.Count == 0)
                continue;
            result.Add(new DtoSeasonalGenre
            {
                GenreId = genre.Id,
                Name = genre.Name,
                ExpiresAt = expiry.Value,
                Entries = entries.OrderByDescending(e => e.EndedAt).ThenByDescending(e => e.ReleaseDate)
                    .ThenByDescending(e => e.PremieredAt).ThenByDescending(e => e.CreatedAt).ThenBy(e => e.Name)
                    .Select(e => Map(e, sources)).ToList()
            });
        }
        await watchedStatus.EnrichAsync(userId, result.SelectMany(x => x.Entries), cancellationToken);
        return result;
    }

    private static DtoMediaEntry Map(MediaBaseEntry entry, long[] sources)
    {
        DtoMediaEntry dto = entry switch
        {
            Movie movie => new DtoMovie
            {
                Collection = movie.MovieCollection is { } collection && sources.Contains(collection.MediaSourceId)
                    ? (DtoMovieCollection)Map(collection, sources) : null
            },
            MovieCollection => new DtoMovieCollection(),
            TVShow => new DtoTVShow(),
            _ => throw new InvalidOperationException("Unsupported seasonal media type.")
        };
        dto.Id = entry.Id;
        dto.Name = entry.Name;
        dto.MediaSourceId = entry.MediaSourceId;
        dto.ReleaseDate = entry.ReleaseDate;
        dto.PremieredAt = entry.PremieredAt;
        dto.EndedAt = entry.EndedAt;
        dto.PosterPictureId = entry.PosterPictureId;
        dto.BannerPictureId = entry.BannerPictureId;
        dto.FanartPictureId = entry.FanartPictureId;
        return dto;
    }
}
