using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Central hook that records "this collection medium got a new child" as a persistent
    /// <see cref="PlaylistBackfillMarker"/> for the automatic playlist backfill
    /// (<see cref="PlaylistBackfillCoordinator"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why here (and not in the scan code): <see cref="SaveChanges(bool)"/> / <see cref="SaveChangesAsync(bool, CancellationToken)"/>
    /// is the single point every EF-based way of creating media passes through - the many creation sites of
    /// <c>MediaSourceClassifier</c>, the metadata editor, demo data and any future path - so none of them has to
    /// remember to mark anything. The rules (which parent gets marked) live in exactly one place:
    /// </para>
    /// <list type="bullet">
    /// <item>new (or re-assigned) <see cref="TVShowEpisode"/> marks its season AND its show (a show entry in a playlist covers all seasons and episodes, a season entry only its episodes);</item>
    /// <item>new (or re-assigned) <see cref="TVShowSeason"/> marks its show;</item>
    /// <item>new <see cref="Movie"/> in a collection, or a movie moved into another collection (changed <see cref="Movie.MovieCollectionId"/>), marks the (new) collection.</item>
    /// </list>
    /// <para>
    /// A parent that is itself created in the same save is not marked (no playlist can contain it yet).
    /// Persistence and atomicity: the marks are written with a bundled upsert
    /// (<c>INSERT ... ON CONFLICT DO UPDATE</c>, version counter + 1) inside the same database transaction as the
    /// media rows, so a rollback leaves no mark behind and a committed child always has its mark. Cost: without
    /// a relevant entity in the change tracker nothing extra happens (no query, no transaction); with relevant
    /// entities the marks are deduplicated in memory (a scan creating thousands of episodes of a few seasons
    /// writes a handful of rows), at most one lookup query resolves the shows of episodes' seasons that are not
    /// tracked, and one (chunked) upsert statement writes them.
    /// </para>
    /// <para>
    /// Limit: only paths that go through <c>SaveChanges</c> are seen. Rows created by raw SQL / bulk statements
    /// (<c>ExecuteSql</c>, <c>ExecuteUpdate</c>, or the backup restore's direct inserts) bypass the hook; the
    /// restore brings its own marker table with it, and everything else is caught by the daily safety sweep.
    /// </para>
    /// </remarks>
    public partial class ApplicationDbContext
    {
        private const int MarkerUpsertChunkSize = 200;
        private const int LookupChunkSize = 500;

        private readonly IPlaylistBackfillSignal? _backfillSignal;

        /// <summary>
        /// Tabelle fuer vorgemerkte Sammel-Medien (neue Kinder, Playlist-Nachlieferung).
        /// </summary>
        public DbSet<PlaylistBackfillMarker> PlaylistBackfillMarkers { get; set; }

        /// <inheritdoc />
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            List<(string MediaType, long MediaId)>? marks;
            int saved;

            using (PauseAutoDetectChanges())
            {
                var candidates = CollectMarkCandidates();
                marks = candidates is null ? null : ResolveMarks(candidates);
                if (marks is null || marks.Count == 0)
                    return base.SaveChanges(acceptAllChangesOnSuccess);

                if (Database.IsRelational())
                {
                    var ownTransaction = Database.CurrentTransaction is null ? Database.BeginTransaction() : null;
                    try
                    {
                        saved = base.SaveChanges(ownTransaction is null && acceptAllChangesOnSuccess);
                        UpsertMarkersSql(marks);
                        ownTransaction?.Commit();
                    }
                    finally
                    {
                        ownTransaction?.Dispose();
                    }

                    if (ownTransaction is not null && acceptAllChangesOnSuccess)
                        ChangeTracker.AcceptAllChanges();

                    _backfillSignal?.NotifyMarkersWritten();
                    return saved;
                }

                saved = base.SaveChanges(acceptAllChangesOnSuccess);
            }

            UpsertMarkersTracked(marks);
            _backfillSignal?.NotifyMarkersWritten();
            return saved;
        }

        /// <inheritdoc />
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            List<(string MediaType, long MediaId)>? marks;
            int saved;

            using (PauseAutoDetectChanges())
            {
                var candidates = CollectMarkCandidates();
                marks = candidates is null ? null : await ResolveMarksAsync(candidates, cancellationToken);
                if (marks is null || marks.Count == 0)
                    return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

                if (Database.IsRelational())
                {
                    var ownTransaction = Database.CurrentTransaction is null
                        ? await Database.BeginTransactionAsync(cancellationToken)
                        : null;
                    try
                    {
                        saved = await base.SaveChangesAsync(ownTransaction is null && acceptAllChangesOnSuccess, cancellationToken);
                        await UpsertMarkersSqlAsync(marks, cancellationToken);
                        if (ownTransaction is not null)
                            await ownTransaction.CommitAsync(cancellationToken);
                    }
                    finally
                    {
                        if (ownTransaction is not null)
                            await ownTransaction.DisposeAsync();
                    }

                    if (ownTransaction is not null && acceptAllChangesOnSuccess)
                        ChangeTracker.AcceptAllChanges();

                    _backfillSignal?.NotifyMarkersWritten();
                    return saved;
                }

                saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            await UpsertMarkersTrackedAsync(marks, cancellationToken);
            _backfillSignal?.NotifyMarkersWritten();
            return saved;
        }

        // Runs change detection once and switches automatic detection off until disposed, so the marking
        // scan and EF's own SaveChanges do not both walk the whole change tracker (a scan's context can track
        // thousands of entities).
        private AutoDetectPause PauseAutoDetectChanges()
        {
            if (!ChangeTracker.AutoDetectChangesEnabled)
                return new AutoDetectPause(null);

            ChangeTracker.DetectChanges();
            ChangeTracker.AutoDetectChangesEnabled = false;
            return new AutoDetectPause(ChangeTracker);
        }

        private readonly struct AutoDetectPause : IDisposable
        {
            private readonly ChangeTracker? _tracker;

            public AutoDetectPause(ChangeTracker? tracker) => _tracker = tracker;

            public void Dispose()
            {
                if (_tracker is not null)
                    _tracker.AutoDetectChangesEnabled = true;
            }
        }

        private sealed class MarkCandidates
        {
            public HashSet<(string MediaType, long MediaId)> Marks { get; } = new();

            // Episode seasons whose show is not known from tracked entities and must be looked up.
            public HashSet<long> SeasonIdsToResolve { get; } = new();
        }

        // Scans the change tracker once for added/re-assigned movies, seasons and episodes. Returns
        // null (no work at all) when there is none.
        private MarkCandidates? CollectMarkCandidates()
        {
            MarkCandidates? candidates = null;
            List<long>? episodeSeasonIds = null;
            Dictionary<long, long>? trackedSeasonShow = null;

            foreach (var entry in ChangeTracker.Entries())
            {
                var state = entry.State;
                switch (entry.Entity)
                {
                    case Movie when state is EntityState.Added or EntityState.Modified:
                        if (TryGetChangedForeignKey(entry, nameof(Movie.MovieCollectionId), state, out var collectionId))
                            (candidates ??= new()).Marks.Add((MediaTypeValues.MovieCollection, collectionId));
                        break;

                    case TVShowSeason season:
                        if (state is EntityState.Added or EntityState.Modified
                            && TryGetChangedForeignKey(entry, nameof(TVShowSeason.TVShowId), state, out var showId))
                        {
                            (candidates ??= new()).Marks.Add((MediaTypeValues.TVShow, showId));
                        }

                        // Tracked season (created or loaded by the scan): lets an episode's show be found without a query.
                        if (state != EntityState.Added && state != EntityState.Detached && season.Id > 0)
                            (trackedSeasonShow ??= new())[season.Id] = season.TVShowId;
                        break;

                    case TVShowEpisode when state is EntityState.Added or EntityState.Modified:
                        if (TryGetChangedForeignKey(entry, nameof(TVShowEpisode.TVShowSeasonId), state, out var seasonId))
                            (episodeSeasonIds ??= new()).Add(seasonId);
                        break;
                }
            }

            if (episodeSeasonIds is not null)
            {
                candidates ??= new();
                foreach (var seasonId in episodeSeasonIds)
                {
                    candidates.Marks.Add((MediaTypeValues.TVShowSeason, seasonId));
                    if (trackedSeasonShow is not null && trackedSeasonShow.TryGetValue(seasonId, out var trackedShowId))
                        candidates.Marks.Add((MediaTypeValues.TVShow, trackedShowId));
                    else
                        candidates.SeasonIdsToResolve.Add(seasonId);
                }
            }

            return candidates;
        }

        // For an added entity: the (persisted, non-temporary) foreign key value; for a modified entity: the
        // value only if that key was actually changed. A temporary key means the parent is created in the
        // same save, so nothing can reference it yet.
        private static bool TryGetChangedForeignKey(EntityEntry entry, string propertyName, EntityState state, out long id)
        {
            id = 0;
            var property = entry.Property(propertyName);
            if (property.IsTemporary)
                return false;
            if (state == EntityState.Modified && !property.IsModified)
                return false;

            if (property.CurrentValue is long value && value > 0)
            {
                id = value;
                return true;
            }

            return false;
        }

        private static List<(string MediaType, long MediaId)> BuildMarks(MarkCandidates candidates, Dictionary<long, long> resolvedSeasonShows)
        {
            foreach (var showId in resolvedSeasonShows.Values)
                candidates.Marks.Add((MediaTypeValues.TVShow, showId));

            return candidates.Marks.ToList();
        }

        private List<(string MediaType, long MediaId)> ResolveMarks(MarkCandidates candidates)
        {
            var resolved = new Dictionary<long, long>();
            foreach (var chunk in candidates.SeasonIdsToResolve.Chunk(LookupChunkSize))
            {
                var rows = TVShowSeasons.AsNoTracking()
                    .Where(s => chunk.Contains(s.Id))
                    .Select(s => new { s.Id, s.TVShowId })
                    .ToList();
                foreach (var row in rows)
                    resolved[row.Id] = row.TVShowId;
            }

            return BuildMarks(candidates, resolved);
        }

        private async Task<List<(string MediaType, long MediaId)>> ResolveMarksAsync(MarkCandidates candidates, CancellationToken cancellationToken)
        {
            var resolved = new Dictionary<long, long>();
            foreach (var chunk in candidates.SeasonIdsToResolve.Chunk(LookupChunkSize))
            {
                var rows = await TVShowSeasons.AsNoTracking()
                    .Where(s => chunk.Contains(s.Id))
                    .Select(s => new { s.Id, s.TVShowId })
                    .ToListAsync(cancellationToken);
                foreach (var row in rows)
                    resolved[row.Id] = row.TVShowId;
            }

            return BuildMarks(candidates, resolved);
        }

        private (string Sql, object[] Parameters) BuildUpsert(IReadOnlyList<(string MediaType, long MediaId)> chunk)
        {
            var table = Model.FindEntityType(typeof(PlaylistBackfillMarker))!.GetTableName();
            var parameters = new List<object> { new SqliteParameter("@at", DateTime.UtcNow) };
            var values = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                parameters.Add(new SqliteParameter($"@t{i}", chunk[i].MediaType));
                parameters.Add(new SqliteParameter($"@i{i}", chunk[i].MediaId));
                values.Add($"(@t{i}, @i{i}, @at, 1)");
            }

            var sql = $"INSERT INTO \"{table}\" (\"MediaType\", \"MediaId\", \"MarkedAt\", \"Version\") VALUES {string.Join(", ", values)} " +
                      "ON CONFLICT(\"MediaType\", \"MediaId\") DO UPDATE SET \"Version\" = \"Version\" + 1, \"MarkedAt\" = excluded.\"MarkedAt\";";
            return (sql, parameters.ToArray());
        }

        private void UpsertMarkersSql(List<(string MediaType, long MediaId)> marks)
        {
            foreach (var chunk in marks.Chunk(MarkerUpsertChunkSize))
            {
                var (sql, parameters) = BuildUpsert(chunk);
                Database.ExecuteSqlRaw(sql, parameters);
            }
        }

        private async Task UpsertMarkersSqlAsync(List<(string MediaType, long MediaId)> marks, CancellationToken cancellationToken)
        {
            foreach (var chunk in marks.Chunk(MarkerUpsertChunkSize))
            {
                var (sql, parameters) = BuildUpsert(chunk);
                await Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
            }
        }

        // Non-relational fallback (EF in-memory provider, tests only): no raw SQL, no transaction.
        private void UpsertMarkersTracked(List<(string MediaType, long MediaId)> marks)
        {
            ApplyTrackedMarks(marks, PlaylistBackfillMarkers.ToList());
            base.SaveChanges();
        }

        private async Task UpsertMarkersTrackedAsync(List<(string MediaType, long MediaId)> marks, CancellationToken cancellationToken)
        {
            ApplyTrackedMarks(marks, await PlaylistBackfillMarkers.ToListAsync(cancellationToken));
            await base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyTrackedMarks(List<(string MediaType, long MediaId)> marks, List<PlaylistBackfillMarker> existing)
        {
            var now = DateTime.UtcNow;
            foreach (var (mediaType, mediaId) in marks)
            {
                var marker = existing.FirstOrDefault(m => m.MediaType == mediaType && m.MediaId == mediaId);
                if (marker is null)
                    PlaylistBackfillMarkers.Add(new PlaylistBackfillMarker { MediaType = mediaType, MediaId = mediaId, MarkedAt = now });
                else
                {
                    marker.Version++;
                    marker.MarkedAt = now;
                }
            }
        }
    }
}
