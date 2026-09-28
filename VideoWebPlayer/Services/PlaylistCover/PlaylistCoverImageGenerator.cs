using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.HomeBackgroundImage;

namespace VideoWebPlayer.Services.PlaylistCover
{
    /// <summary>
    /// Generates a composite cover image (collage) for a playlist from the poster pictures of its
    /// contents, mirroring <see cref="HomeBackgroundImageGenerator"/>: this class only collects images and
    /// composes the collage bytes, it does not persist anything - saving the resulting <see cref="Picture"/>
    /// and updating <see cref="Playlist.CoverPictureId"/> is <see cref="PlaylistService.GeneratePlaylistCoverAsync"/>'s
    /// responsibility, exactly as <c>HomeBackgroundImageGenerator.GenerateAsync</c> hands its composed bytes
    /// to its caller without writing to the database itself.
    /// </summary>
    public class PlaylistCoverImageGenerator
    {
        /// <summary>
        /// The maximum number of images collected for a single collage.
        /// </summary>
        public const int MaxImages = 5;

        // Cross-fade width reused from HomeBackgroundImageGenerator's own default, for a visually
        // consistent collage style across the application.
        private const int TransitionWidth = 32;

        private readonly ApplicationDbContext _db;
        private readonly PlaylistSettings _settings;
        private readonly ILogger<PlaylistCoverImageGenerator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistCoverImageGenerator"/> class.
        /// </summary>
        /// <param name="db">Database context.</param>
        /// <param name="settings">Playlist configuration, providing the collage's target dimensions and JPEG quality.</param>
        /// <param name="logger">Logger instance.</param>
        public PlaylistCoverImageGenerator(ApplicationDbContext db, IOptions<PlaylistSettings> settings, ILogger<PlaylistCoverImageGenerator> logger)
        {
            _db = db;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Generates a JPEG collage from up to <see cref="MaxImages"/> poster pictures of the playlist's
        /// entries.
        ///
        /// DESIGN DECISION (Schritt 10, Anforderung): Images are collected by media-type <b>priority</b>,
        /// not by the entries' playlist order. Priority order: TVShow (and TVShowSeason, which falls back
        /// to its parent TVShow's poster - seasons have no poster of their own) before TVShowEpisode,
        /// before MovieCollection, before Movie. Concretely: every TVShow/TVShowSeason entry with a poster
        /// is collected first, then every TVShowEpisode entry, then MovieCollection, then Movie - until
        /// <see cref="MaxImages"/> images are collected. The physical order entries were added to the
        /// playlist only decides which entry within the same priority tier is considered first (oldest
        /// first), it never lets a later-priority entry (e.g. a Movie added first) jump ahead of an
        /// earlier-priority one (e.g. a TVShow added later).
        ///
        /// This method itself performs no automatic/periodic regeneration: it is only ever invoked by
        /// <see cref="PlaylistService.GeneratePlaylistCoverAsync"/>, which is only reachable from the
        /// explicit "Neu erzeugen" UI action - see the design decision documented there.
        /// </summary>
        /// <param name="playlistId">The playlist to generate a cover collage for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The composed collage, JPEG-encoded, or <see langword="null"/> if no source images are available or generation failed.</returns>
        public async Task<byte[]?> GeneratePlaylistCoverAsync(long playlistId, CancellationToken cancellationToken = default)
        {
            try
            {
                var orderedImageData = await CollectOrderedImageDataAsync(playlistId, cancellationToken);
                if (orderedImageData.Count == 0)
                    return null;

                var width = _settings.GeneratedCoverWidthPixels;
                var height = _settings.GeneratedCoverHeightPixels;
                var quality = _settings.GeneratedCoverJpegQuality;

                return await Task.Run(
                    () => HomeBackgroundImageGenerator.Compose(orderedImageData, width, height, TransitionWidth, quality),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Konsistent mit EpisodeBackgroundImageGenerator/HomeBackgroundImageGenerator: Fehler bei der
                // Collagen-Erzeugung fuehren zu null statt einer Exception, die UI zeigt dann den Platzhalter.
                _logger.LogError(ex, "Fehler bei der Generierung des Playlist-Covers fuer Playlist {PlaylistId}.", playlistId);
                return null;
            }
        }

        /// <summary>
        /// Collects the raw poster image bytes of a playlist's entries, in media-type priority order (see
        /// the design decision on <see cref="GeneratePlaylistCoverAsync"/>), up to <see cref="MaxImages"/>.
        /// </summary>
        /// <param name="playlistId">The playlist to collect entries for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The collected image bytes, in priority order.</returns>
        private async Task<List<byte[]>> CollectOrderedImageDataAsync(long playlistId, CancellationToken cancellationToken)
        {
            var pictureIds = await CollectOrderedPictureIdsAsync(playlistId, cancellationToken);
            if (pictureIds.Count == 0)
                return new List<byte[]>();

            var pictureData = await _db.Pictures.AsNoTracking()
                .Where(p => pictureIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Data, cancellationToken);

            return pictureIds
                .Select(id => pictureData.TryGetValue(id, out var data) ? data : null)
                .Where(data => data is not null && data.Length > 0)
                .Select(data => data!)
                .ToList();
        }

        /// <summary>
        /// Resolves the (deduplicated) poster picture ids of a playlist's entries, in media-type priority
        /// order, up to <see cref="MaxImages"/>.
        /// </summary>
        /// <param name="playlistId">The playlist to collect entries for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved picture ids, in priority order.</returns>
        /// <remarks>
        /// Internal (not private) so <c>PlaylistCoverGeneratorTests</c> can assert the resolved priority
        /// order directly by picture id, instead of having to decode composed JPEG collage pixels to infer
        /// which source images were used and in what order.
        /// </remarks>
        internal async Task<List<long>> CollectOrderedPictureIdsAsync(long playlistId, CancellationToken cancellationToken = default)
        {
            var entries = await _db.PlaylistEntries.AsNoTracking()
                .Where(e => e.PlaylistId == playlistId)
                .OrderBy(e => e.AddedAt)
                .ThenBy(e => e.Id)
                .Select(e => new { e.MediaType, e.MediaId })
                .ToListAsync(cancellationToken);

            if (entries.Count == 0)
                return new List<long>();

            // Stabile Sortierung nach Prioritaet: innerhalb derselben Prioritaetsstufe bleibt die
            // urspruengliche (Hinzufuege-)Reihenfolge erhalten - siehe Designentscheidung oben.
            var prioritized = entries
                .Select(e => new { e.MediaType, e.MediaId, Priority = GetMediaTypePriority(e.MediaType) })
                .Where(e => e.Priority < int.MaxValue)
                .OrderBy(e => e.Priority)
                .ToList();

            if (prioritized.Count == 0)
                return new List<long>();

            var posterLookup = await BuildPosterLookupAsync(prioritized.Select(e => (e.MediaType, e.MediaId)), cancellationToken);

            var orderedPictureIds = new List<long>(MaxImages);
            foreach (var entry in prioritized)
            {
                if (orderedPictureIds.Count >= MaxImages)
                    break;

                if (posterLookup.TryGetValue((entry.MediaType, entry.MediaId), out var pictureId)
                    && pictureId.HasValue
                    && !orderedPictureIds.Contains(pictureId.Value))
                {
                    orderedPictureIds.Add(pictureId.Value);
                }
            }

            return orderedPictureIds;
        }

        /// <summary>
        /// Priority tier for a playlist entry's media type, lower values collected first (see the design
        /// decision on <see cref="GeneratePlaylistCoverAsync"/>). <see cref="MediaTypeValues.TVShowSeason"/>
        /// shares TVShow's tier since it resolves to the same (parent TVShow's) poster.
        /// </summary>
        /// <param name="mediaType">The playlist entry's media type.</param>
        /// <returns>The priority tier, or <see cref="int.MaxValue"/> for an unknown/unsupported media type.</returns>
        private static int GetMediaTypePriority(string mediaType) => mediaType switch
        {
            MediaTypeValues.TVShow => 0,
            MediaTypeValues.TVShowSeason => 0,
            MediaTypeValues.TVShowEpisode => 1,
            MediaTypeValues.MovieCollection => 2,
            MediaTypeValues.Movie => 3,
            _ => int.MaxValue
        };

        /// <summary>
        /// Bulk-resolves each (media type, media id) reference's poster picture id: <c>PosterPictureId</c>
        /// for TVShow, TVShowEpisode, MovieCollection and Movie; for TVShowSeason, the <b>parent TVShow's</b>
        /// <c>PosterPictureId</c> (seasons have no poster of their own - see the design decision on
        /// <see cref="GeneratePlaylistCoverAsync"/>).
        /// </summary>
        /// <param name="references">The (media type, media id) references to resolve poster picture ids for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A dictionary of (media type, media id) to resolved poster picture id (possibly <see langword="null"/>).</returns>
        private async Task<Dictionary<(string MediaType, long MediaId), long?>> BuildPosterLookupAsync(
            IEnumerable<(string MediaType, long MediaId)> references, CancellationToken cancellationToken)
        {
            var result = new Dictionary<(string, long), long?>();

            var byType = references
                .GroupBy(r => r.MediaType)
                .ToDictionary(g => g.Key, g => g.Select(r => r.MediaId).Distinct().ToList());

            if (byType.TryGetValue(MediaTypeValues.TVShow, out var tvShowIds))
            {
                var posters = await _db.TVShows.AsNoTracking()
                    .Where(t => tvShowIds.Contains(t.Id))
                    .ToDictionaryAsync(t => t.Id, t => t.PosterPictureId, cancellationToken);
                foreach (var id in tvShowIds)
                    result[(MediaTypeValues.TVShow, id)] = posters.TryGetValue(id, out var p) ? p : null;
            }

            if (byType.TryGetValue(MediaTypeValues.TVShowEpisode, out var episodeIds))
            {
                var posters = await _db.TVShowEpisodes.AsNoTracking()
                    .Where(e => episodeIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id, e => e.PosterPictureId, cancellationToken);
                foreach (var id in episodeIds)
                    result[(MediaTypeValues.TVShowEpisode, id)] = posters.TryGetValue(id, out var p) ? p : null;
            }

            if (byType.TryGetValue(MediaTypeValues.MovieCollection, out var collectionIds))
            {
                var posters = await _db.MovieCollections.AsNoTracking()
                    .Where(c => collectionIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.PosterPictureId, cancellationToken);
                foreach (var id in collectionIds)
                    result[(MediaTypeValues.MovieCollection, id)] = posters.TryGetValue(id, out var p) ? p : null;
            }

            if (byType.TryGetValue(MediaTypeValues.Movie, out var movieIds))
            {
                var posters = await _db.Movies.AsNoTracking()
                    .Where(m => movieIds.Contains(m.Id))
                    .ToDictionaryAsync(m => m.Id, m => m.PosterPictureId, cancellationToken);
                foreach (var id in movieIds)
                    result[(MediaTypeValues.Movie, id)] = posters.TryGetValue(id, out var p) ? p : null;
            }

            if (byType.TryGetValue(MediaTypeValues.TVShowSeason, out var seasonIds))
            {
                var seasons = await _db.TVShowSeasons.AsNoTracking()
                    .Where(s => seasonIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.TVShowId })
                    .ToListAsync(cancellationToken);

                var missingShowIds = seasons.Select(s => s.TVShowId).Distinct()
                    .Where(showId => !result.ContainsKey((MediaTypeValues.TVShow, showId)))
                    .ToList();
                var showPosters = missingShowIds.Count == 0
                    ? new Dictionary<long, long?>()
                    : await _db.TVShows.AsNoTracking()
                        .Where(t => missingShowIds.Contains(t.Id))
                        .ToDictionaryAsync(t => t.Id, t => t.PosterPictureId, cancellationToken);

                foreach (var season in seasons)
                {
                    var showPoster = result.TryGetValue((MediaTypeValues.TVShow, season.TVShowId), out var known)
                        ? known
                        : showPosters.TryGetValue(season.TVShowId, out var loaded) ? loaded : null;
                    result[(MediaTypeValues.TVShowSeason, season.Id)] = showPoster;
                }
            }

            return result;
        }
    }
}
