using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using VideoWebPlayer.Events;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Der zentrale Datenbankkontext für die Anwendung.
    /// Verwaltet Identität, MediaSources, MediaCollections und MediaItems.
    /// Kapselt außerdem Event-Publishing für CRUD-Operationen.
    /// </summary>
    public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        // Instanz des EventManagers für die Event-basierte Architektur
        private readonly EventManager _eventManager;

        /// <summary>
        /// Erstellt eine neue Instanz des ApplicationDbContext.
        /// </summary>
        /// <param name="options">Konfigurationsoptionen für den DbContext.</param>
        /// <param name="eventManager">EventManager für das Publizieren von Events.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, EventManager eventManager)
            : base(options)
        {
            _eventManager = eventManager;
        }
        #region DbSet Properties
        /// <summary>
        /// Tabelle für MediaSources (Quellen).
        /// </summary>
        public DbSet<MediaSource> MediaSources { get; set; }
        /// <summary>
        /// Tabelle für MediaSource-User-Zuordnungen.
        /// </summary>
        public DbSet<MediaSourceUser> MediaSourceUsers { get; set; }

        /// <summary>
        /// Tabelle für MediaCollections (Sammlungen).
        /// </summary>
        public DbSet<MediaCollection> MediaCollections { get; set; }

        /// <summary>
        /// Tabelle für MediaItems (Medienobjekte).
        /// </summary>
        public DbSet<MediaItem> MediaItems { get; set; }

        /// <summary>
        /// Tabelle für MovieCollections.
        /// </summary>
        public DbSet<MovieCollection> MovieCollections { get; set; }
        /// <summary>
        /// Tabelle für Movies.
        /// </summary>
        public DbSet<Movie> Movies { get; set; }
        /// <summary>
        /// Tabelle für TVShows.
        /// </summary>
        public DbSet<TVShow> TVShows { get; set; }
        /// <summary>
        /// Tabelle für TVShow-Seasons.
        /// </summary>
        public DbSet<TVShowSeason> TVShowSeasons { get; set; }
        /// <summary>
        /// Tabelle für TVShow-Episodes.
        /// </summary>
        public DbSet<TVShowEpisode> TVShowEpisodes { get; set; }
        /// <summary>
        /// Tabelle für Movie-MediaItem-Verknüpfungen.
        /// </summary>
        public DbSet<MovieMediaItem> MovieMediaItems { get; set; }
        /// <summary>
        /// Tabelle für TVShowEpisode-MediaItem-Verknüpfungen.
        /// </summary>
        public DbSet<TVShowEpisodeMediaItem> TVShowEpisodeMediaItems { get; set; }
        /// <summary>
        /// Tabelle für Bilder.
        /// </summary>
        public DbSet<Picture> Pictures { get; set; }

		/// <summary>
		/// Table for uploaded source icon images.
		/// </summary>
		public DbSet<MediaSourceIcon> MediaSourceIcons { get; set; }
        /// <summary>
        /// Tabelle für Setup-Einträge.
        /// </summary>
        public DbSet<Setup> Setups { get; set; }
        /// <summary>
        /// Tabelle für RecentEntries.
        /// </summary>
        public DbSet<RecentEntry> RecentEntries { get; set; }
        /// <summary>
        /// Tabelle für Favoriten.
        /// </summary>
        public DbSet<FavoriteEntry> FavoriteEntries { get; set; }
        /// <summary>
        /// Tabelle für Genres.
        /// </summary>
        public DbSet<Genre> Genres { get; set; }
        /// <summary>
        /// Tabelle für alternative Genre-Namen.
        /// </summary>
        public DbSet<GenreName> GenreNames { get; set; }
        /// <summary>
        /// Tabelle für Movie-Genre-Verknüpfungen.
        /// </summary>
        public DbSet<MovieGenre> MovieGenres { get; set; }
        /// <summary>
        /// Tabelle für TVShow-Genre-Verknüpfungen.
        /// </summary>
        public DbSet<TVShowGenre> TVShowGenres { get; set; }
        /// <summary>
        /// Tabelle für ContinueWatching-Einträge.
        /// </summary>
        public DbSet<ContinueWatchingEntry> ContinueWatchingEntries { get; set; }
        /// <summary>
        /// Tabelle für gesperrte Login-IPs.
        /// </summary>
        public DbSet<BlockedLoginIp> BlockedLoginIps { get; set; }   // NEU
        #endregion
        #region MediaSource Manipulation Methods
        /// <summary>
        /// Fügt eine neue MediaSource hinzu, speichert sie und publiziert ein Event.
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
        /// Löscht eine MediaSource sowie alle zugehörigen MediaCollections und MediaItems rekursiv.
        /// Publiziert nach erfolgreichem Löschen ein Event.
        /// </summary>
        /// <param name="source">Die zu löschende MediaSource.</param>
        public async Task DeleteMediaSourceAsync(MediaSource source)
        {
            var existingSource = await MediaSources.FindAsync(source.Id);
            if (existingSource != null)
            {
                // Hole alle Collections der Quelle
                var collections = await MediaCollections
                    .Where(c => c.MediaSourceId == source.Id)
                    .ToListAsync();

                await DeleteMediaCollectionsForSourceAsync(collections);

                MediaSources.Remove(existingSource);
                await SaveChangesAsync();
                _eventManager.Publish(new MediaSourceDeletedEvent(source));
            }
            
        }

        /// <summary>
        /// Löscht eine MediaCollection, alle untergeordneten Collections und alle zugehörigen MediaItems rekursiv.
        /// </summary>
        /// <param name="collection">Die zu löschende MediaCollection.</param>
        private async Task DeleteMediaCollectionsForSourceAsync(List<MediaCollection> collections)
        {
            if (collections.Count == 0)
                return;

            // Iterativer Post-Order Traversal (stack-safe) zum Löschen (Children -> Parent)
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

            // Falls es "hängende" Nodes gibt (z.B. fehlerhafte Parent-Referenzen), trotzdem löschen.
            foreach (var c in collections)
            {
                if (!visited.Contains(c.Id))
                    Traverse(c);
            }

            var collectionIds = collections.Select(c => c.Id).ToList();

            // Lösche alle MediaItems dieser Collections
            var items = await MediaItems
                .Where(i => collectionIds.Contains(i.MediaCollectionId))
                .ToListAsync();
            MediaItems.RemoveRange(items);

            // Lösche Collections children-first
            foreach (var collection in deleteOrder)
                MediaCollections.Remove(collection);
        }
        #endregion

        /// <summary>
        /// Stellt sicher, dass eine MediaCollection mit gegebener MediaSourceId und Path existiert.
        /// Gibt die bestehende Collection zurück oder legt sie neu an.
        /// </summary>
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
        /// Gibt das bestehende Item zurück oder legt es neu an.
        /// Wird ein bestehendes Item gefunden und das CreatedAt-Datum ist unterschiedlich, wird es aktualisiert und Changed auf true gesetzt.
        /// </summary>
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
        /// Ergebnisobjekt mit den möglichen zugehörigen Entitäten für ein MediaItem.
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
        /// Lädt die zu einer MediaItem-Id gehörenden übergeordneten Entitäten (MediaCollection, Movie (+MovieCollection) oder TVShow/Season/Episode).
        /// Gibt ein <see cref="MediaItemRelationResult"/> mit gefüllten Properties zurück (nicht gefundene bleiben null).
        /// </summary>
        public async Task<MediaItemRelationResult> GetRelationsForMediaItemAsync(long mediaItemId, CancellationToken cancellationToken = default)
        {
            var result = new MediaItemRelationResult();

            // Lade MediaItem mit zugehöriger MediaCollection
            var mediaItem = await MediaItems
                .Include(mi => mi.MediaCollection)
                .FirstOrDefaultAsync(mi => mi.Id == mediaItemId, cancellationToken);

            if (mediaItem == null)
                return result;

            result.MediaCollection = mediaItem.MediaCollection;

            // Prüfe, ob das MediaItem mit einem Movie verknüpft ist
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

            // Prüfe, ob das MediaItem mit einer TVShowEpisode verknüpft ist
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
        }
    }
}
