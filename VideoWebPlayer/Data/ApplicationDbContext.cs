using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
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
        public DbSet<MediaSourceUser> MediaSourceUsers { get; set; }

        /// <summary>
        /// Tabelle für MediaCollections (Sammlungen).
        /// </summary>
        public DbSet<MediaCollection> MediaCollections { get; set; }

        /// <summary>
        /// Tabelle für MediaItems (Medienobjekte).
        /// </summary>
        public DbSet<MediaItem> MediaItems { get; set; }

        public DbSet<MovieCollection> MovieCollections { get; set; }
        public DbSet<Movie> Movies { get; set; }
        public DbSet<TVShow> TVShows { get; set; }
        public DbSet<TVShowSeason> TVShowSeasons { get; set; }
        public DbSet<TVShowEpisode> TVShowEpisodes { get; set; }
        public DbSet<MovieMediaItem> MovieMediaItems { get; set; }
        public DbSet<TVShowEpisodeMediaItem> TVShowEpisodeMediaItems { get; set; }
        public DbSet<Picture> Pictures { get; set; }
        public DbSet<Setup> Setups { get; set; }
        public DbSet<RecentEntry> RecentEntries { get; set; }
        public DbSet<FavoriteEntry> FavoriteEntries { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<GenreName> GenreNames { get; set; }
        public DbSet<MovieGenre> MovieGenres { get; set; }
        public DbSet<TVShowGenre> TVShowGenres { get; set; }
        public DbSet<ContinueWatchingEntry> ContinueWatchingEntries { get; set; }
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

                foreach (var collection in collections)
                {
                    await DeleteMediaCollectionRecursiveAsync(collection);
                }

                MediaSources.Remove(existingSource);
                await SaveChangesAsync();
                _eventManager.Publish(new MediaSourceDeletedEvent(source));
            }
            
        }

        /// <summary>
        /// Löscht eine MediaCollection, alle untergeordneten Collections und alle zugehörigen MediaItems rekursiv.
        /// </summary>
        /// <param name="collection">Die zu löschende MediaCollection.</param>
        private async Task DeleteMediaCollectionRecursiveAsync(MediaCollection collection)
        {
            // Lösche alle MediaItems dieser Collection
            var items = await MediaItems
                .Where(i => i.MediaCollectionId == collection.Id)
                .ToListAsync();
            MediaItems.RemoveRange(items);

            // Hole und lösche alle Unter-Collections rekursiv
            var childCollections = await MediaCollections
                .Where(c => c.ParentMediaCollectionId == collection.Id)
                .ToListAsync();

            foreach (var child in childCollections)
            {
                await DeleteMediaCollectionRecursiveAsync(child);
            }

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

        #region Favoriten
        public bool IsFavorite(string userId, MediaBaseEntry entry)
        {
            if (entry is Movie)
                return FavoriteEntries.Any(f => f.UserId == userId && f.MovieId == entry.Id);
            else if (entry is MovieCollection)
                return FavoriteEntries.Any(f => f.UserId == userId && f.MovieCollectionId == entry.Id);
            else if (entry is TVShow)
                return FavoriteEntries.Any(f => f.UserId == userId && f.TVShowId == entry.Id);
            else if (entry is TVShowSeason)
                return FavoriteEntries.Any(f => f.UserId == userId && f.TVShowSeasonId == entry.Id);
            else if (entry is TVShowEpisode)
                return FavoriteEntries.Any(f => f.UserId == userId && f.TVShowEpisodeId == entry.Id);
            return false;
        }
        public async Task AddFavoriteAsync(string userId, MediaBaseEntry entry)
        {
            if (IsFavorite(userId, entry))
                return;
            var newFav = new FavoriteEntry()
            {
                UserId = userId,
                MovieId = entry is Movie ? entry.Id : null,
                MovieCollectionId = entry is MovieCollection ? entry.Id : null,
                TVShowId = entry is TVShow ? entry.Id : null,
                TVShowSeasonId = entry is TVShowSeason ? entry.Id : null,
                TVShowEpisodeId = entry is TVShowEpisode ? entry.Id : null
            };
            FavoriteEntries.Add(newFav);
            await SaveChangesAsync();
        }
        public async Task RemoveFavoriteAsync(string userId, MediaBaseEntry entry)
        {
            var existing = (entry is Movie) ? FavoriteEntries.FirstOrDefault(f => f.UserId == userId && f.MovieId == entry.Id)
                : (entry is MovieCollection) ? FavoriteEntries.FirstOrDefault(f => f.UserId == userId && f.MovieCollectionId == entry.Id)
                : (entry is TVShow) ? FavoriteEntries.FirstOrDefault(f => f.UserId == userId && f.TVShowId == entry.Id)
                : (entry is TVShowSeason) ? FavoriteEntries.FirstOrDefault(f => f.UserId == userId && f.TVShowSeasonId == entry.Id)
                : (entry is TVShowEpisode) ? FavoriteEntries.FirstOrDefault(f => f.UserId == userId && f.TVShowEpisodeId == entry.Id)
                : null;
            if (existing is not null)
                FavoriteEntries.Remove(existing);
            await SaveChangesAsync();
        }
        public async Task<bool> ToggleFavoriteAsync(string userId, MediaBaseEntry entry)
        {
            if (IsFavorite(userId, entry))
            {
                await RemoveFavoriteAsync(userId, entry);
                return false; // Favorit entfernt
            }
            else
            {
                await AddFavoriteAsync(userId, entry);
                return true; // Favorit hinzugefügt
            }
        }
        #endregion

        public async Task MarkGenresAsChangedAsync()
        {
            var setup = await Setups.FirstOrDefaultAsync();
            if (setup != null)
            {
                setup.GenresChanged = true;
                await SaveChangesAsync();
            }
        }

        public IEnumerable<Genre> GetSeasonalGenres()
        {
            var now = DateTime.UtcNow.Date;
            var genres = Genres.ToList();

            foreach (var genre in genres)
            {
                if (genre.StartDate == null || genre.EndDate == null)
                    continue;

                var start = genre.StartDate.Value;
                var end = genre.EndDate.Value;

                // Falls Enddatum in der Vergangenheit, Zeitraum um Jahre erhöhen bis aktuell
                while (end < now)
                {
                    start = start.AddYears(1);
                    end = end.AddYears(1);
                }

                if (start <= now && end >= now)
                    yield return genre;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // MediaCollection → MediaSource (viele zu eins)
            modelBuilder.Entity<MediaCollection>()
                .HasOne(mc => mc.MediaSource)
                .WithMany(ms => ms.MediaCollections)
                .HasForeignKey(mc => mc.MediaSourceId);

            // MediaCollection → ParentMediaCollection (rekursive Beziehung, viele zu eins)
            modelBuilder.Entity<MediaCollection>()
                .HasOne(mc => mc.ParentMediaCollection)
                .WithMany(mc => mc.ChildCollections)
                .HasForeignKey(mc => mc.ParentMediaCollectionId)
                .OnDelete(DeleteBehavior.Restrict); // oder Cascade, je nach gewünschtem Verhalten

            // MediaItem → MediaCollection (viele zu eins)
            modelBuilder.Entity<MediaItem>()
                .HasOne(mi => mi.MediaCollection)
                .WithMany(mc => mc.MediaItems)
                .HasForeignKey(mi => mi.MediaCollectionId);

            modelBuilder.Entity<Movie>()
                .HasOne(m => m.MovieCollection)
                .WithMany(mc => mc.Movies)
                .HasForeignKey(m => m.MovieCollectionId);

            modelBuilder.Entity<TVShowSeason>()
                .HasOne(s => s.TVShow)
                .WithMany(t => t.Seasons)
                .HasForeignKey(s => s.TVShowId);

            modelBuilder.Entity<TVShowEpisode>()
                .HasOne(e => e.TVShowSeason)
                .WithMany(s => s.Episodes)
                .HasForeignKey(e => e.TVShowSeasonId);

            modelBuilder.Entity<MovieMediaItem>()
                .HasKey(x => new { x.MovieId, x.MediaItemId });

            modelBuilder.Entity<MovieMediaItem>()
                .HasOne(x => x.Movie)
                .WithMany()
                .HasForeignKey(x => x.MovieId);

            modelBuilder.Entity<MovieMediaItem>()
                .HasOne(x => x.MediaItem)
                .WithMany()
                .HasForeignKey(x => x.MediaItemId);

            modelBuilder.Entity<TVShowEpisodeMediaItem>()
                .HasKey(x => new { x.TVShowEpisodeId, x.MediaItemId });

            modelBuilder.Entity<TVShowEpisodeMediaItem>()
                .HasOne(x => x.TVShowEpisode)
                .WithMany()
                .HasForeignKey(x => x.TVShowEpisodeId);

            modelBuilder.Entity<TVShowEpisodeMediaItem>()
                .HasOne(x => x.MediaItem)
                .WithMany()
                .HasForeignKey(x => x.MediaItemId);

            modelBuilder.Entity<MovieGenre>()
                .HasKey(mg => new { mg.MovieId, mg.GenreId });
            modelBuilder.Entity<MovieGenre>()
                .HasOne(mg => mg.Movie)
                .WithMany(m => m.MovieGenres)
                .HasForeignKey(mg => mg.MovieId);
            modelBuilder.Entity<MovieGenre>()
                .HasOne(mg => mg.Genre)
                .WithMany(g => g.MovieGenres)
                .HasForeignKey(mg => mg.GenreId);

            modelBuilder.Entity<TVShowGenre>()
                .HasKey(tg => new { tg.TVShowId, tg.GenreId });
            modelBuilder.Entity<TVShowGenre>()
                .HasOne(tg => tg.TVShow)
                .WithMany(t => t.TVShowGenres)
                .HasForeignKey(tg => tg.TVShowId);
            modelBuilder.Entity<TVShowGenre>()
                .HasOne(tg => tg.Genre)
                .WithMany(g => g.TVShowGenres)
                .HasForeignKey(tg => tg.GenreId);

            modelBuilder.Entity<MediaSourceUser>()
                .HasKey(msu => new { msu.MediaSourceId, msu.UserId });

            modelBuilder.Entity<ContinueWatchingEntry>(e =>
            {
                e.HasIndex(x => new { x.UserId, x.MovieId });
                e.HasIndex(x => new { x.UserId, x.TVShowEpisodeId });

                e.HasOne(x => x.Movie)
                 .WithMany()
                 .HasForeignKey(x => x.MovieId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.TVShowEpisode)
                 .WithMany()
                 .HasForeignKey(x => x.TVShowEpisodeId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
