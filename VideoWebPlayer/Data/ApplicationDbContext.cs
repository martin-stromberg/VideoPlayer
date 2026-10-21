using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using VideoWebPlayer.Events;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Der zentrale Datenbankkontext fï¿½r die Anwendung.
    /// Verwaltet Identitï¿½t, MediaSources, MediaCollections und MediaItems.
    /// Kapselt auï¿½erdem Event-Publishing fï¿½r CRUD-Operationen.
    /// </summary>
    public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        // Instanz des EventManagers fï¿½r die Event-basierte Architektur
        private readonly EventManager _eventManager;
        private readonly IProgramScheduleSignal? _programScheduleSignal;

        /// <summary>
        /// Erstellt eine neue Instanz des ApplicationDbContext.
        /// </summary>
        /// <param name="options">Konfigurationsoptionen fï¿½r den DbContext.</param>
        /// <param name="eventManager">EventManager fï¿½r das Publizieren von Events.</param>
        /// <param name="backfillSignal">Optional wake-up line for the playlist backfill worker (null in tests / when unused).</param>
        /// <param name="programScheduleSignal">Optionale Weckleitung f&#252;r den Sendeplan-Worker (null in Tests / wenn ungenutzt).</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, EventManager eventManager, IPlaylistBackfillSignal? backfillSignal = null, IProgramScheduleSignal? programScheduleSignal = null)
            : base(options)
        {
            _eventManager = eventManager;
            _backfillSignal = backfillSignal;
            _programScheduleSignal = programScheduleSignal;

            // Die per [DbFunction]/HasDbFunction() angebundene Funktion lower_invariant() existiert nicht
            // von sich aus in SQLite; sie muss zusaetzlich auf der zugrunde liegenden Verbindung als
            // benutzerdefinierte Funktion registriert werden, damit EF Core sie tatsaechlich ausfuehren kann.
            if (Database.IsRelational() && Database.GetDbConnection() is SqliteConnection sqliteConnection)
                sqliteConnection.CreateFunction<string?, string?>("lower_invariant", AppDbFunctions.LowerInvariant);
        }
        #region DbSet Properties
        /// <summary>
        /// Tabelle fï¿½r MediaSources (Quellen).
        /// </summary>
        public DbSet<MediaSource> MediaSources { get; set; }
        /// <summary>
        /// Tabelle fï¿½r MediaSource-User-Zuordnungen.
        /// </summary>
        public DbSet<MediaSourceUser> MediaSourceUsers { get; set; }

        /// <summary>
        /// Tabelle fï¿½r MediaCollections (Sammlungen).
        /// </summary>
        public DbSet<MediaCollection> MediaCollections { get; set; }

        /// <summary>
        /// Tabelle fï¿½r MediaItems (Medienobjekte).
        /// </summary>
        public DbSet<MediaItem> MediaItems { get; set; }

        /// <summary>
        /// Tabelle fï¿½r MovieCollections.
        /// </summary>
        public DbSet<MovieCollection> MovieCollections { get; set; }
        /// <summary>
        /// Tabelle fï¿½r Movies.
        /// </summary>
        public DbSet<Movie> Movies { get; set; }
        /// <summary>
        /// Tabelle fï¿½r TVShows.
        /// </summary>
        public DbSet<TVShow> TVShows { get; set; }
        /// <summary>
        /// Tabelle fï¿½r TVShow-Seasons.
        /// </summary>
        public DbSet<TVShowSeason> TVShowSeasons { get; set; }
        /// <summary>
        /// Tabelle fï¿½r TVShow-Episodes.
        /// </summary>
        public DbSet<TVShowEpisode> TVShowEpisodes { get; set; }
        /// <summary>
        /// Tabelle fï¿½r Movie-MediaItem-Verknï¿½pfungen.
        /// </summary>
        public DbSet<MovieMediaItem> MovieMediaItems { get; set; }
        /// <summary>
        /// Tabelle fï¿½r TVShowEpisode-MediaItem-Verknï¿½pfungen.
        /// </summary>
        public DbSet<TVShowEpisodeMediaItem> TVShowEpisodeMediaItems { get; set; }
        /// <summary>
        /// Tabelle fï¿½r Bilder.
        /// </summary>
        public DbSet<Picture> Pictures { get; set; }

        /// <summary>
        /// Table for uploaded source icon images.
        /// </summary>
        public DbSet<MediaSourceIcon> MediaSourceIcons { get; set; }
        /// <summary>
        /// Tabelle fï¿½r Setup-Eintrï¿½ge.
        /// </summary>
        public DbSet<Setup> Setups { get; set; }
        /// <summary>
        /// Tabelle fï¿½r RecentEntries.
        /// </summary>
        public DbSet<RecentEntry> RecentEntries { get; set; }
        /// <summary>
        /// Tabelle fï¿½r Favoriten.
        /// </summary>
        public DbSet<FavoriteEntry> FavoriteEntries { get; set; }
        /// <summary>
        /// Tabelle fÃ¼r Playlists.
        /// </summary>
        public DbSet<Playlist> Playlists { get; set; }
        /// <summary>
        /// Tabelle fÃ¼r Playlist-EintrÃ¤ge.
        /// </summary>
        public DbSet<PlaylistEntry> PlaylistEntries { get; set; }
        /// <summary>
        /// Tabelle fÃ¼r Playlist-Eintraege, die der Anwender bewusst einzeln aus einer Playlist entfernt hat
        /// und die deshalb von der automatischen Nachlieferung (<see cref="Services.PlaylistBackfillService"/>)
        /// nicht erneut hinzugefuegt werden sollen.
        /// </summary>
        public DbSet<PlaylistEntryExclusion> PlaylistEntryExclusions { get; set; }
        /// <summary>
        /// Tabelle fÃ¼r die (automatisch abgeleiteten oder manuell Ã¼berschriebenen) Genres einer Playlist
        /// (<see cref="Services.PlaylistGenreService"/>).
        /// </summary>
        public DbSet<PlaylistGenre> PlaylistGenres { get; set; }
        /// <summary>
        /// Tabelle fÃ¼r einzeln freigeschaltete MedieneintrÃ¤ge.
        /// </summary>
        public DbSet<UnlockedMediaEntry> UnlockedMediaEntries { get; set; }
        /// <summary>
        /// Tabelle fï¿½r Genres.
        /// </summary>
        public DbSet<Genre> Genres { get; set; }
        /// <summary>
        /// Tabelle fï¿½r alternative Genre-Namen.
        /// </summary>
        public DbSet<GenreName> GenreNames { get; set; }
        /// <summary>
        /// Tabelle fï¿½r Movie-Genre-Verknï¿½pfungen.
        /// </summary>
        public DbSet<MovieGenre> MovieGenres { get; set; }
        /// <summary>
        /// Tabelle fï¿½r TVShow-Genre-Verknï¿½pfungen.
        /// </summary>
        public DbSet<TVShowGenre> TVShowGenres { get; set; }
        /// <summary>
        /// Tabelle fï¿½r Schauspieler.
        /// </summary>
        public DbSet<Actor> Actors { get; set; }
        /// <summary>
        /// Tabelle fï¿½r Movie-Actor-Verknï¿½pfungen.
        /// </summary>
        public DbSet<MovieActor> MovieActors { get; set; }
        /// <summary>
        /// Tabelle fï¿½r TVShowEpisode-Actor-Verknï¿½pfungen.
        /// </summary>
        public DbSet<TVShowEpisodeActor> TVShowEpisodeActors { get; set; }
        /// <summary>
        /// Tabelle fï¿½r ContinueWatching-Eintrï¿½ge.
        /// </summary>
        public DbSet<ContinueWatchingEntry> ContinueWatchingEntries { get; set; }
        /// <summary>
        /// Tabelle fuer Gesehen-Markierungen.
        /// </summary>
        public DbSet<WatchedEntry> WatchedEntries { get; set; }
        /// <summary>
        /// Tabelle fï¿½r gesperrte Login-IPs.
        /// </summary>
        public DbSet<BlockedLoginIp> BlockedLoginIps { get; set; }   // NEU
        /// <summary>
        /// Tabelle fuer Backup-Einstellungen.
        /// </summary>
        public DbSet<BackupSettings> BackupSettings { get; set; }
        /// <summary>
        /// Tabelle fuer Backup- und Restore-Historie.
        /// </summary>
        public DbSet<BackupOperationHistory> BackupOperationHistories { get; set; }
        /// <summary>
        /// Tabelle fuer Update-Einstellungen.
        /// </summary>
        public DbSet<UpdateSettings> UpdateSettings { get; set; }
        /// <summary>
        /// Tabelle fuer gekoppelte Geraete.
        /// </summary>
        public DbSet<PairedDevice> PairedDevices { get; set; }
        /// <summary>
        /// Tabelle fuer Einmal-Pairing-Codes.
        /// </summary>
        public DbSet<PairingCode> PairingCodes { get; set; }
        /// <summary>
        /// Tabelle fuer Refresh-Tokens (gekoppelt an Benutzer und Geraet).
        /// </summary>
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        /// <summary>
        /// Tabelle für die automatischen TV-Sender.
        /// </summary>
        public DbSet<TVChannel> TVChannels { get; set; }
        /// <summary>
        /// Tabelle für die Programmregeln der TV-Sender.
        /// </summary>
        public DbSet<ProgramRule> ProgramRules { get; set; }

        /// <summary>
        /// Tabelle für Sendeplätze (Sendeplan des TV-Senders).
        /// </summary>
        public DbSet<ProgramSlot> ProgramSlots { get; set; }

        /// <summary>
        /// Tabelle für die Fortsetzungsstände der Programmregeln.
        /// </summary>
        public DbSet<ProgramRuleCursor> ProgramRuleCursors { get; set; }
        #endregion
        #region MediaSource Manipulation Methods
        /// <summary>
        /// Fï¿½gt eine neue MediaSource hinzu, speichert sie und publiziert ein Event.
        /// </summary>
        /// <param name="source">Die neue MediaSource.</param>
        public async Task AddMediaSourceAsync(MediaSource source)
        {
            source.CreatedAt = DateTime.UtcNow;
            MediaSources.Add(source);
            await SaveChangesAsync();
            _eventManager.Publish(new MediaSourceCreatedEvent(source));
        }

        /// <summary>
        /// Aktualisiert eine bestehende MediaSource und publiziert ein Event.
        /// </summary>
        /// <param name="source">Die zu aktualisierende MediaSource.</param>
        public async Task UpdateMediaSourceAsync(MediaSource source)
        {
            var existingSource = await MediaSources.FindAsync(source.Id);
            if (existingSource != null)
            {
                existingSource.Update(source);
                await SaveChangesAsync();
                _eventManager.Publish(new MediaSourceUpdatedEvent(source));
            }
        }

        /// <summary>
        /// Lï¿½scht eine MediaSource sowie alle zugehï¿½rigen MediaCollections und MediaItems rekursiv.
        /// Publiziert nach erfolgreichem Lï¿½schen ein Event.
        /// </summary>
        /// <param name="source">Die zu lï¿½schende MediaSource.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="beforeContinueWatchingCleanupAsync">
        /// Optional hook invoked, within this method's own transaction, immediately before the affected
        /// <see cref="ContinueWatchingEntry"/> rows are unconditionally deleted. Exists so a higher service
        /// layer (see <c>IPlaylistService.ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync</c>)
        /// can first try to re-point playlist-bound continue-watching entries at another, still-existing
        /// title of the same playlist, instead of this method's unconditional deletion always winning; this
        /// data-access layer intentionally has no knowledge of that playlist/sort/accessibility logic
        /// itself, only of when it must run for the result to stay transactionally consistent with the rest
        /// of the source deletion. <see langword="null"/> (e.g. from tests, or callers that do not care
        /// about this replacement behavior) simply skips the hook, preserving this method's previous,
        /// unconditional-delete-only behavior.
        /// </param>
        public async Task DeleteMediaSourceAsync(
            MediaSource source,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default,
            Func<CancellationToken, Task>? beforeContinueWatchingCleanupAsync = null)
        {
            ArgumentNullException.ThrowIfNull(source);

            var existingSource = await MediaSources.FindAsync(new object[] { source.Id }, cancellationToken);
            if (existingSource is null)
                return;

            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

            Entry(existingSource).State = EntityState.Detached;

            var step = 0;
            const int TotalSteps = 22;

            void Report()
            {
                step++;
                progress?.Report((double)step / TotalSteps);
            }

            try
            {
                await WatchedEntries
                    .Where(we => (we.Movie != null && we.Movie.MediaSourceId == source.Id) ||
                                 (we.TVShowEpisode != null && we.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId == source.Id))
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                if (beforeContinueWatchingCleanupAsync is not null)
                    await beforeContinueWatchingCleanupAsync(cancellationToken);

                await ContinueWatchingEntries
                    .Where(cwe => (cwe.Movie != null && cwe.Movie.MediaSourceId == source.Id) ||
                                  (cwe.TVShowEpisode != null && cwe.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId == source.Id))
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await FavoriteEntries
                    .Where(fe => Movies.Any(m => m.MediaSourceId == source.Id && m.Id == fe.MovieId) ||
                                MovieCollections.Any(mc => mc.MediaSourceId == source.Id && mc.Id == fe.MovieCollectionId) ||
                                TVShows.Any(t => t.MediaSourceId == source.Id && t.Id == fe.TVShowId) ||
                                TVShowSeasons.Any(s => s.TVShow.MediaSourceId == source.Id && s.Id == fe.TVShowSeasonId) ||
                                TVShowEpisodes.Any(e => e.TVShowSeason.TVShow.MediaSourceId == source.Id && e.Id == fe.TVShowEpisodeId))
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await RecentEntries
                    .Where(re => re.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await MovieMediaItems
                    .Where(mmi => mmi.MediaItem.MediaCollection.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await TVShowEpisodeMediaItems
                    .Where(ei => ei.MediaItem.MediaCollection.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await TVShowGenres
                    .Where(tg => tg.TVShow.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await MovieGenres
                    .Where(mg => mg.Movie.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await MovieActors
                    .Where(ma => ma.Movie.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await TVShowEpisodeActors
                    .Where(ea => ea.TVShowEpisode.TVShowSeason.TVShow.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await Pictures
                    .Where(p => p.EpisodeId != null &&
                                TVShowEpisodes.Any(e => e.Id == p.EpisodeId.Value &&
                                                        e.TVShowSeason.TVShow.MediaSourceId == source.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.EpisodeId, (long?)null), cancellationToken);
                Report();

                await TVShowEpisodes
                    .Where(e => e.TVShowSeason.TVShow.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await Movies
                    .Where(m => m.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await TVShowSeasons
                    .Where(s => s.TVShow.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await TVShows
                    .Where(t => t.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await MovieCollections
                    .Where(mc => mc.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await Pictures
                    .Where(p => p.MediaItem.MediaCollection.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                var collections = await MediaCollections
                    .Where(mc => mc.MediaSourceId == source.Id)
                    .ToListAsync(cancellationToken);
                if (collections.Count > 0)
                {
                    await DeleteMediaCollectionsForSourceAsync(collections, cancellationToken);
                    await SaveChangesAsync(cancellationToken);
                }
                Report();

                await GenreNames
                    .Where(gn => gn.Genre.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await Genres
                    .Where(g => g.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await MediaSourceUsers
                    .Where(msu => msu.MediaSourceId == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await MediaSources
                    .Where(ms => ms.Id == source.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                Report();

                await transaction.CommitAsync(cancellationToken);
                _eventManager.Publish(new MediaSourceDeletedEvent(source));
                _programScheduleSignal?.NotifyScheduleChanged();
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        /// <summary>
        /// Lï¿½scht eine MediaCollection, alle untergeordneten Collections und alle zugehï¿½rigen MediaItems rekursiv.
        /// </summary>
        /// <param name="collections">Die zu lï¿½schenden MediaCollections.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task DeleteMediaCollectionsForSourceAsync(List<MediaCollection> collections, CancellationToken cancellationToken = default)
        {
            if (collections.Count == 0)
                return;

            // Iterativer Post-Order Traversal (stack-safe) zum Lï¿½schen (Children -> Parent)
            var collectionsById = collections.ToDictionary(c => c.Id);
            var childrenByParentId = collections
                .Where(c => c.ParentMediaCollectionId.HasValue)
                .GroupBy(c => c.ParentMediaCollectionId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var deleteOrder = new List<MediaCollection>(collections.Count);
            var visited = new HashSet<long>();

            IEnumerable<MediaCollection> roots = collections.Where(c =>
                !c.ParentMediaCollectionId.HasValue ||
                !collectionsById.ContainsKey(c.ParentMediaCollectionId.Value));

            void Traverse(MediaCollection root)
            {
                var stack = new Stack<(MediaCollection Collection, bool Expanded)>();
                stack.Push((root, false));

                while (stack.Count > 0)
                {
                    var (current, expanded) = stack.Pop();

                    if (expanded)
                    {
                        deleteOrder.Add(current);
                        continue;
                    }

                    if (!visited.Add(current.Id))
                        continue;

                    stack.Push((current, true));

                    if (childrenByParentId.TryGetValue(current.Id, out var children))
                    {
                        foreach (var child in children)
                            stack.Push((child, false));
                    }
                }
            }

            foreach (var root in roots)
                Traverse(root);

            // Falls es "hï¿½ngende" Nodes gibt (z.B. fehlerhafte Parent-Referenzen), trotzdem lï¿½schen.
            foreach (var c in collections)
            {
                if (!visited.Contains(c.Id))
                    Traverse(c);
            }

            // Lï¿½sche Collections children-first
            foreach (var collection in deleteOrder)
                MediaCollections.Remove(collection);
        }
        #endregion

        /// <summary>
        /// Stellt sicher, dass eine MediaCollection mit gegebener MediaSourceId und Path existiert.
        /// Gibt die bestehende Collection zurï¿½ck oder legt sie neu an.
        /// </summary>
        /// <param name="collection">Die zu suchende bzw. anzulegende Collection.</param>
        /// <param name="cancellationToken">Token zum Abbrechen der Operation.</param>
        /// <returns>Die bestehende oder neu angelegte Collection.</returns>
        public async Task<MediaCollection> EnsureMediaCollectionExistsAsync(MediaCollection collection, CancellationToken cancellationToken = default)
        {
            var existing = await MediaCollections
                .FirstOrDefaultAsync(c =>
                    c.MediaSourceId == collection.MediaSourceId &&
                    c.Path == collection.Path,
                    cancellationToken);

            if (existing != null)
                return existing;

            MediaCollections.Add(collection);
            await SaveChangesAsync(cancellationToken);
            return collection;
        }

        /// <summary>
        /// Stellt sicher, dass ein MediaItem mit gegebener MediaCollectionId und Path existiert.
        /// Gibt das bestehende Item zurï¿½ck oder legt es neu an.
        /// Wird ein bestehendes Item gefunden und das CreatedAt-Datum ist unterschiedlich, wird es aktualisiert und Changed auf true gesetzt.
        /// </summary>
        /// <param name="item">Das zu suchende bzw. anzulegende MediaItem.</param>
        /// <param name="cancellationToken">Token zum Abbrechen der Operation.</param>
        /// <returns>Das bestehende oder neu angelegte MediaItem.</returns>
        public async Task<MediaItem> EnsureMediaItemExistsAsync(MediaItem item, CancellationToken cancellationToken = default)
        {
            var existing = await MediaItems
                .FirstOrDefaultAsync(i =>
                    i.MediaCollectionId == item.MediaCollectionId &&
                    i.Path == item.Path,
                    cancellationToken);

            if (existing != null)
            {
                item.CreatedAt = new DateTime(item.CreatedAt.Year, item.CreatedAt.Month, item.CreatedAt.Day, item.CreatedAt.Hour, item.CreatedAt.Minute, 0, 0);
                if (existing.CreatedAt != item.CreatedAt)
                {
                    existing.CreatedAt = item.CreatedAt;
                    existing.Changed = true;
                    await SaveChangesAsync(cancellationToken);
                }
                return existing;
            }
            item.CreatedAt = new DateTime(item.CreatedAt.Year, item.CreatedAt.Month, item.CreatedAt.Day, item.CreatedAt.Hour, item.CreatedAt.Minute, 0, 0);
            MediaItems.Add(item);
            await SaveChangesAsync(cancellationToken);
            return item;
        }

        /// <summary>
        /// Ensures a movie collection exists for the specified source and name.
        /// </summary>
        /// <param name="collection">The collection to ensure.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The existing or created collection.</returns>
        public async Task<MovieCollection> EnsureMovieCollectionExistsAsync(MovieCollection collection, CancellationToken cancellationToken = default)
        {
            var existing = await MovieCollections
                .FirstOrDefaultAsync(c => c.Name == collection.Name && c.MediaSourceId == collection.MediaSourceId, cancellationToken);

            if (existing != null)
                return existing;

            MovieCollections.Add(collection);
            await SaveChangesAsync(cancellationToken);
            return collection;
        }

        /// <summary>
        /// Ensures a movie exists for the specified source and name.
        /// </summary>
        /// <param name="movie">The movie to ensure.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The existing or created movie.</returns>
        public async Task<Movie> EnsureMovieExistsAsync(Movie movie, CancellationToken cancellationToken = default)
        {
            var existing = await Movies
                .FirstOrDefaultAsync(m => m.Name == movie.Name && m.MediaSourceId == movie.MediaSourceId, cancellationToken);

            if (existing != null)
                return existing;

            Movies.Add(movie);
            await SaveChangesAsync(cancellationToken);
            return movie;
        }

        /// <summary>
        /// Ensures a TV show exists for the specified source and name.
        /// </summary>
        /// <param name="show">The TV show to ensure.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The existing or created TV show.</returns>
        public async Task<TVShow> EnsureTVShowExistsAsync(TVShow show, CancellationToken cancellationToken = default)
        {
            var existing = await TVShows
                .FirstOrDefaultAsync(s => s.Name == show.Name && s.MediaSourceId == show.MediaSourceId, cancellationToken);

            if (existing != null)
                return existing;

            TVShows.Add(show);
            await SaveChangesAsync(cancellationToken);
            return show;
        }

        /// <summary>
        /// Ensures a TV show season exists for the specified show and name.
        /// </summary>
        /// <param name="season">The season to ensure.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The existing or created season.</returns>
        public async Task<TVShowSeason> EnsureTVShowSeasonExistsAsync(TVShowSeason season, CancellationToken cancellationToken = default)
        {
            var existing = await TVShowSeasons
                .FirstOrDefaultAsync(s => s.Name == season.Name && s.TVShowId == season.TVShowId, cancellationToken);

            if (existing != null)
                return existing;

            TVShowSeasons.Add(season);
            await SaveChangesAsync(cancellationToken);
            return season;
        }

        /// <summary>
        /// Ensures a TV show episode exists for the specified season and name.
        /// </summary>
        /// <param name="episode">The episode to ensure.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The existing or created episode.</returns>
        public async Task<TVShowEpisode> EnsureTVShowEpisodeExistsAsync(TVShowEpisode episode, CancellationToken cancellationToken = default)
        {
            var existing = await TVShowEpisodes
                .FirstOrDefaultAsync(e => e.Name == episode.Name && e.TVShowSeasonId == episode.TVShowSeasonId, cancellationToken);

            if (existing != null)
                return existing;

            TVShowEpisodes.Add(episode);
            await SaveChangesAsync(cancellationToken);
            return episode;
        }

        /// <summary>
        /// Ergebnisobjekt mit den mï¿½glichen zugehï¿½rigen Entitï¿½ten fï¿½r ein MediaItem.
        /// </summary>
        public class MediaItemRelationResult
        {
            /// <summary>
            /// The media collection that contains the media item, if any.
            /// </summary>
            public MediaCollection? MediaCollection { get; set; }

            /// <summary>
            /// The movie linked to the media item, if any.
            /// </summary>
            public Movie? Movie { get; set; }

            /// <summary>
            /// The movie collection that contains the movie, if any.
            /// </summary>
            public MovieCollection? MovieCollection { get; set; }

            /// <summary>
            /// The TV show associated with the media item (via episode -> season -> show), if any.
            /// </summary>
            public TVShow? TVShow { get; set; }

            /// <summary>
            /// The TV show season associated with the media item, if any.
            /// </summary>
            public TVShowSeason? TVShowSeason { get; set; }

            /// <summary>
            /// The TV show episode associated with the media item, if any.
            /// </summary>
            public TVShowEpisode? TVShowEpisode { get; set; }
        }

        /// <summary>
        /// Lï¿½dt die zu einer MediaItem-Id gehï¿½renden ï¿½bergeordneten Entitï¿½ten (MediaCollection, Movie (+MovieCollection) oder TVShow/Season/Episode).
        /// Gibt ein <see cref="MediaItemRelationResult"/> mit gefï¿½llten Properties zurï¿½ck (nicht gefundene bleiben null).
        /// </summary>
        /// <param name="mediaItemId">Die Id des MediaItems, dessen Beziehungen geladen werden sollen.</param>
        /// <param name="cancellationToken">Token zum Abbrechen der Operation.</param>
        /// <returns>Das <see cref="MediaItemRelationResult"/> mit den geladenen Beziehungen.</returns>
        public async Task<MediaItemRelationResult> GetRelationsForMediaItemAsync(long mediaItemId, CancellationToken cancellationToken = default)
        {
            var result = new MediaItemRelationResult();

            // Lade MediaItem mit zugehï¿½riger MediaCollection
            var mediaItem = await MediaItems
                .Include(mi => mi.MediaCollection)
                .FirstOrDefaultAsync(mi => mi.Id == mediaItemId, cancellationToken);

            if (mediaItem == null)
                return result;

            result.MediaCollection = mediaItem.MediaCollection;

            // Prï¿½fe, ob das MediaItem mit einem Movie verknï¿½pft ist
            var movieLink = await MovieMediaItems
                .Include(mmi => mmi.Movie)
                    .ThenInclude(m => m.MovieCollection)
                .FirstOrDefaultAsync(mmi => mmi.MediaItemId == mediaItemId, cancellationToken);

            if (movieLink != null)
            {
                result.Movie = movieLink.Movie;
                result.MovieCollection = movieLink.Movie?.MovieCollection;
                return result;
            }

            // Prï¿½fe, ob das MediaItem mit einer TVShowEpisode verknï¿½pft ist
            var episodeLink = await TVShowEpisodeMediaItems
                .Include(ei => ei.TVShowEpisode)
                    .ThenInclude(ep => ep.TVShowSeason)
                        .ThenInclude(s => s.TVShow)
                .FirstOrDefaultAsync(ei => ei.MediaItemId == mediaItemId, cancellationToken);

            if (episodeLink != null)
            {
                result.TVShowEpisode = episodeLink.TVShowEpisode;
                result.TVShowSeason = episodeLink.TVShowEpisode?.TVShowSeason;
                result.TVShow = episodeLink.TVShowEpisode?.TVShowSeason?.TVShow;
            }

            return result;
        }
        /// <summary>
        /// Configures model relationships and constraints.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            modelBuilder.HasDbFunction(typeof(AppDbFunctions).GetMethod(nameof(AppDbFunctions.LowerInvariant))!);
        }
    }
}
