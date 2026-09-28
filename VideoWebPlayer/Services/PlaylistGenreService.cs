using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements the genre-derivation business logic (Entwicklungsschritt 9): a playlist's genres are, by
/// default, automatically derived from the genres of the titles it currently contains, recomputed every
/// time the playlist's contents change (<see cref="RecomputeGenresAsync"/>, called by
/// <see cref="PlaylistService"/> after every add/remove and by <see cref="PlaylistBackfillService"/> after
/// every automatic backfill) - unless the owner has manually overridden the selection
/// (<see cref="Playlist.GenresManuallyOverridden"/>, set via <see cref="SetManualGenresAsync"/>), in which
/// case the derivation is skipped until <see cref="ResetGenresAsync"/> is called.
/// </summary>
/// <remarks>
/// Not registered in DI - constructed directly by <see cref="PlaylistService"/> and
/// <see cref="PlaylistBackfillService"/> against their own <see cref="ApplicationDbContext"/> instance,
/// mirroring how those two classes already construct <see cref="PlaylistEntryReorderService"/>.
/// <para>
/// Every recompute is a full "throw away and rebuild from the current contents" pass rather than an
/// incremental update keyed off the specific entries added/removed: the cost scales with the playlist's
/// own entry count (already loaded by the caller for the add/remove itself) plus the number of distinct
/// underlying movies/TV shows it references, which is bounded by that same entry count - simple, obviously
/// correct, and not grossly inefficient for the playlist sizes this application deals with.
/// </para>
/// </remarks>
internal sealed class PlaylistGenreService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistGenreService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    public PlaylistGenreService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Recomputes <paramref name="playlist"/>'s <see cref="PlaylistGenre"/> rows from its current contents
    /// (a no-op while <see cref="Playlist.GenresManuallyOverridden"/> is set), and persists the result.
    /// </summary>
    /// <param name="playlist">
    /// The playlist to recompute genres for - its current <see cref="Playlist.PlaylistEntries"/> must
    /// already be persisted (this reads them back from <see cref="_db"/>, not from the given instance's
    /// possibly-stale navigation property).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RecomputeGenresAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        if (playlist.GenresManuallyOverridden)
            return;

        var counts = await ComputeGenreCountsAsync(playlist.Id, cancellationToken);
        await ReplacePlaylistGenresAsync(playlist.Id, counts, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Overrides <paramref name="playlist"/>'s genres with exactly <paramref name="genreIds"/> (unknown ids
    /// are silently ignored), sets <see cref="Playlist.GenresManuallyOverridden"/>, and persists both. From
    /// this point on, <see cref="RecomputeGenresAsync"/> is a no-op for this playlist until
    /// <see cref="ResetGenresAsync"/> is called.
    /// </summary>
    /// <param name="playlist">The playlist to override the genres of.</param>
    /// <param name="genreIds">The genre ids to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetManualGenresAsync(Playlist playlist, IReadOnlyCollection<long> genreIds, CancellationToken cancellationToken = default)
    {
        var distinctGenreIds = genreIds.Distinct().ToList();
        var validGenreIds = distinctGenreIds.Count == 0
            ? new List<long>()
            : await _db.Genres.AsNoTracking().Where(g => distinctGenreIds.Contains(g.Id)).Select(g => g.Id).ToListAsync(cancellationToken);

        // Manually picked genres carry no frequency signal, so every row gets the same Count - display
        // order then falls back to the genre name (see the DtoPlaylist conversion in PlaylistService).
        await ReplacePlaylistGenresAsync(playlist.Id, validGenreIds.ToDictionary(id => id, _ => 1), cancellationToken);

        playlist.GenresManuallyOverridden = true;
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Clears <paramref name="playlist"/>'s <see cref="Playlist.GenresManuallyOverridden"/> flag and
    /// immediately recomputes its genres from its current contents, persisting both.
    /// </summary>
    /// <param name="playlist">The playlist to reset the genres of.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ResetGenresAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        playlist.GenresManuallyOverridden = false;
        playlist.UpdatedAt = DateTime.UtcNow;

        var counts = await ComputeGenreCountsAsync(playlist.Id, cancellationToken);
        await ReplacePlaylistGenresAsync(playlist.Id, counts, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Bulk-loads the <see cref="PlaylistGenre"/> rows (genre id, genre name and frequency count) of the
    /// given playlists in a single query, grouped by playlist id - used by <see cref="PlaylistService"/>'s
    /// DTO conversion to build both the capped, frequency-sorted display list and the full id set (for
    /// prefilling the manual-override editor) without a separate query per playlist.
    /// </summary>
    /// <param name="playlistIds">The playlist ids to load genre data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of playlist id to that playlist's (genre id, genre name, count) rows.</returns>
    public async Task<Dictionary<long, List<(long GenreId, string GenreName, int Count)>>> LoadPlaylistGenresAsync(
        IReadOnlyCollection<long> playlistIds, CancellationToken cancellationToken = default)
    {
        if (playlistIds.Count == 0)
            return new Dictionary<long, List<(long GenreId, string GenreName, int Count)>>();

        var rows = await _db.PlaylistGenres.AsNoTracking()
            .Where(pg => playlistIds.Contains(pg.PlaylistId))
            .Select(pg => new { pg.PlaylistId, pg.GenreId, GenreName = pg.Genre.Name, pg.Count })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.PlaylistId)
            .ToDictionary(g => g.Key, g => g.Select(r => (r.GenreId, r.GenreName, r.Count)).ToList());
    }

    /// <summary>
    /// Computes the (genre id -&gt; distinct-title count) map for a playlist's current contents: for each
    /// entry, resolves the movie(s)/TV show it is genre-relevant for (a movie collection's contained
    /// movies via <see cref="MediaHierarchyRegistry"/>'s cascade-children lookup, already used for the
    /// same purpose by <see cref="PlaylistService.AddMediaToPlaylistAsync"/> and
    /// <see cref="PlaylistBackfillService"/>; a season's or episode's parent TV show), then counts, per
    /// genre, how many distinct underlying movies/TV shows carry it - not how many playlist entries do, so
    /// a TV show added together with several of its own episodes does not inflate its genres' frequency
    /// relative to a single movie.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The genre id to distinct-title-count map.</returns>
    private async Task<Dictionary<long, int>> ComputeGenreCountsAsync(long playlistId, CancellationToken cancellationToken)
    {
        var entries = await _db.PlaylistEntries.AsNoTracking()
            .Where(e => e.PlaylistId == playlistId)
            .Select(e => new { e.MediaType, e.MediaId })
            .ToListAsync(cancellationToken);

        var movieIds = new HashSet<long>();
        var tvShowIds = new HashSet<long>();

        foreach (var entry in entries)
        {
            switch (entry.MediaType)
            {
                case MediaTypeValues.Movie:
                    movieIds.Add(entry.MediaId);
                    break;
                case MediaTypeValues.TVShow:
                    tvShowIds.Add(entry.MediaId);
                    break;
            }
        }

        var movieCollectionIds = entries.Where(e => e.MediaType == MediaTypeValues.MovieCollection).Select(e => e.MediaId).ToList();
        foreach (var collectionId in movieCollectionIds)
        {
            var handler = MediaHierarchyRegistry.Handlers[MediaType.MovieCollection];
            var children = await handler.LoadCascadeChildrenAsync!(_db, collectionId, cancellationToken);
            foreach (var (_, movieId) in children)
                movieIds.Add(movieId);
        }

        var tvShowSeasonIds = entries.Where(e => e.MediaType == MediaTypeValues.TVShowSeason).Select(e => e.MediaId).ToList();
        if (tvShowSeasonIds.Count > 0)
        {
            var showIdsFromSeasons = await _db.TVShowSeasons.AsNoTracking()
                .Where(s => tvShowSeasonIds.Contains(s.Id))
                .Select(s => s.TVShowId)
                .ToListAsync(cancellationToken);
            foreach (var showId in showIdsFromSeasons)
                tvShowIds.Add(showId);
        }

        var tvShowEpisodeIds = entries.Where(e => e.MediaType == MediaTypeValues.TVShowEpisode).Select(e => e.MediaId).ToList();
        if (tvShowEpisodeIds.Count > 0)
        {
            var showIdsFromEpisodes = await _db.TVShowEpisodes.AsNoTracking()
                .Where(e => tvShowEpisodeIds.Contains(e.Id))
                .Select(e => e.TVShowSeason.TVShowId)
                .ToListAsync(cancellationToken);
            foreach (var showId in showIdsFromEpisodes)
                tvShowIds.Add(showId);
        }

        var counts = new Dictionary<long, int>();

        if (movieIds.Count > 0)
        {
            var movieGenreCounts = await _db.MovieGenres.AsNoTracking()
                .Where(mg => movieIds.Contains(mg.MovieId))
                .GroupBy(mg => mg.GenreId)
                .Select(g => new { GenreId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);
            foreach (var g in movieGenreCounts)
                counts[g.GenreId] = counts.GetValueOrDefault(g.GenreId) + g.Count;
        }

        if (tvShowIds.Count > 0)
        {
            var showGenreCounts = await _db.TVShowGenres.AsNoTracking()
                .Where(tg => tvShowIds.Contains(tg.TVShowId))
                .GroupBy(tg => tg.GenreId)
                .Select(g => new { GenreId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);
            foreach (var g in showGenreCounts)
                counts[g.GenreId] = counts.GetValueOrDefault(g.GenreId) + g.Count;
        }

        return counts;
    }

    /// <summary>
    /// Replaces every existing <see cref="PlaylistGenre"/> row of the given playlist with rows built from
    /// <paramref name="counts"/> (a full delete-then-insert, not a diff - see the class remarks on why
    /// that is an acceptable, deliberately simple choice here). Does not call
    /// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> itself; callers persist together with
    /// their own additional changes (e.g. <see cref="Playlist.GenresManuallyOverridden"/>).
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="counts">The genre id to count map to build the new rows from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ReplacePlaylistGenresAsync(long playlistId, Dictionary<long, int> counts, CancellationToken cancellationToken)
    {
        var existing = await _db.PlaylistGenres.Where(pg => pg.PlaylistId == playlistId).ToListAsync(cancellationToken);
        if (existing.Count > 0)
            _db.PlaylistGenres.RemoveRange(existing);

        foreach (var (genreId, count) in counts)
        {
            _db.PlaylistGenres.Add(new PlaylistGenre
            {
                PlaylistId = playlistId,
                GenreId = genreId,
                Count = count
            });
        }
    }
}
