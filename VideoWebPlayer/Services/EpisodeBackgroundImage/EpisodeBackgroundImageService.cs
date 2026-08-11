using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.EpisodeBackgroundImage
{
    /// <summary>
    /// Business logic for lazily generating, persisting and caching episode background images.
    /// Thread-safe against parallel requests for the same episode.
    /// </summary>
    public class EpisodeBackgroundImageService
    {
        private static readonly ConcurrentDictionary<long, AsyncLock> EpisodeLocks = new();

        private readonly ApplicationDbContext _db;
        private readonly EpisodeBackgroundImageGenerator _generator;
        private readonly IMemoryCache _cache;
        private readonly EpisodeBackgroundImageOptions _options;
        private readonly ILogger<EpisodeBackgroundImageService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpisodeBackgroundImageService"/> class.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="generator">The image generator.</param>
        /// <param name="cache">The memory cache.</param>
        /// <param name="options">The episode background image options.</param>
        /// <param name="logger">Logger instance.</param>
        public EpisodeBackgroundImageService(
            ApplicationDbContext db,
            EpisodeBackgroundImageGenerator generator,
            IMemoryCache cache,
            IOptions<EpisodeBackgroundImageOptions> options,
            ILogger<EpisodeBackgroundImageService> logger)
        {
            _db = db;
            _generator = generator;
            _cache = cache;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Ensures a background image exists for the given episode, generating and persisting it lazily if necessary.
        /// </summary>
        /// <param name="episode">The episode to ensure a background image for.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The generated or existing <see cref="Picture"/>, or <c>null</c> if no background image is available.</returns>
        public async Task<Picture?> EnsureBackgroundImageAsync(TVShowEpisode episode, CancellationToken cancellationToken)
        {
            if (episode is null)
                throw new ArgumentNullException(nameof(episode));

            var existingPicture = await TryGetExistingPictureAsync(episode, useCache: true, cancellationToken);
            if (existingPicture is not null)
                return existingPicture;

            using (await EpisodeLocks.GetOrAdd(episode.Id, _ => new AsyncLock()).LockAsync(cancellationToken))
            {
                var currentEpisode = await _db.TVShowEpisodes
                    .FirstOrDefaultAsync(e => e.Id == episode.Id, cancellationToken);
                if (currentEpisode is null)
                    return null;

                existingPicture = await TryGetExistingPictureAsync(currentEpisode, useCache: false, cancellationToken);
                if (existingPicture is not null)
                    return existingPicture;

                var sourcePicture = await TryLoadBackgroundSourcePictureAsync(currentEpisode, cancellationToken);
                if (sourcePicture is null)
                    return null;

                return await GenerateAndPersistBackgroundPictureAsync(currentEpisode, sourcePicture, cancellationToken);
            }
        }

        /// <summary>
        /// Attempts to load the currently referenced generated background picture for the given episode,
        /// caching its ID when found. Returns <c>null</c> if the episode has no up-to-date generated picture.
        /// </summary>
        /// <param name="episode">The episode to check.</param>
        /// <param name="useCache">Whether the in-memory cache should be consulted for the picture ID first.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The existing <see cref="Picture"/>, or <c>null</c> if none is available.</returns>
        private async Task<Picture?> TryGetExistingPictureAsync(TVShowEpisode episode, bool useCache, CancellationToken cancellationToken)
        {
            if (!episode.GeneratedBackgroundPictureId.HasValue || episode.BackgroundImageRequiresUpdate)
                return null;

            var pictureId = episode.GeneratedBackgroundPictureId.Value;
            if (useCache)
            {
                var cachedId = await TryGetCachedImageIdAsync(episode.Id);
                pictureId = cachedId ?? pictureId;
            }

            var existingPicture = await TryLoadExistingPictureAsync(pictureId, cancellationToken);
            if (existingPicture is not null)
                CachePictureId(episode.Id, existingPicture.Id);

            return existingPicture;
        }

        private Task<Picture?> TryLoadExistingPictureAsync(long pictureId, CancellationToken cancellationToken)
            => _db.Pictures.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pictureId, cancellationToken);

        /// <summary>
        /// Loads the episode's fanart picture, if present and usable; otherwise falls back to the
        /// episode's poster picture. Returns <c>null</c> if neither is available or has usable image data.
        /// </summary>
        /// <param name="episode">The episode to load a background source picture for.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The fanart or poster <see cref="Picture"/> to use as background source, or <c>null</c> if none is available.</returns>
        private async Task<Picture?> TryLoadBackgroundSourcePictureAsync(TVShowEpisode episode, CancellationToken cancellationToken)
        {
            if (!episode.FanartPictureId.HasValue && !episode.PosterPictureId.HasValue)
                return null;

            var fanartPicture = !episode.FanartPictureId.HasValue ? null : await _db.Pictures.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == episode.FanartPictureId.Value, cancellationToken);
            if (fanartPicture is not null && fanartPicture.Data is not null && fanartPicture.Data.Length > 0)
                return fanartPicture;

            var posterPicture = !episode.PosterPictureId.HasValue ? null : await _db.Pictures.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == episode.PosterPictureId.Value, cancellationToken);
            return posterPicture is null || posterPicture.Data is null || posterPicture.Data.Length == 0
                ? null
                : posterPicture;
        }

        /// <summary>
        /// Generates a new background picture from the given source picture (fanart or poster), persists it,
        /// removes the obsolete generated picture (if any) and updates the episode's background image state.
        /// </summary>
        /// <param name="episode">The episode the background picture is generated for.</param>
        /// <param name="sourcePicture">The fanart or poster picture to generate the background from.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The generated and persisted <see cref="Picture"/>, or <c>null</c> if generation failed.</returns>
        private async Task<Picture?> GenerateAndPersistBackgroundPictureAsync(TVShowEpisode episode, Picture sourcePicture, CancellationToken cancellationToken)
        {
            var generated = await TryGenerateBackgroundPictureAsync(episode, sourcePicture, cancellationToken);
            if (generated is null)
                return null;

            generated.MediaItemId = sourcePicture.MediaItemId;
            generated.EpisodeId = episode.Id;

            await RemoveObsoleteGeneratedPictureAsync(episode, cancellationToken);

            _db.Pictures.Add(generated);
            await _db.SaveChangesAsync(cancellationToken);

            episode.GeneratedBackgroundPictureId = generated.Id;
            episode.BackgroundImageGeneratedAt = DateTime.UtcNow;
            episode.BackgroundImageRequiresUpdate = false;
            await _db.SaveChangesAsync(cancellationToken);

            CachePictureId(episode.Id, generated.Id);
            return generated;
        }

        private async Task<Picture?> TryGenerateBackgroundPictureAsync(TVShowEpisode episode, Picture sourcePicture, CancellationToken cancellationToken)
        {
            try
            {
                return await _generator.GenerateBackgroundImageAsync(episode, sourcePicture.Data, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (_options.EnableLogging)
                    _logger.LogError(ex, "Fehler bei der Generierung des Hintergrundbilds für Episode {EpisodeId}.", episode.Id);
                return null;
            }
        }

        private async Task RemoveObsoleteGeneratedPictureAsync(TVShowEpisode episode, CancellationToken cancellationToken)
        {
            if (!episode.GeneratedBackgroundPictureId.HasValue)
                return;

            var obsoletePicture = await _db.Pictures
                .FirstOrDefaultAsync(p => p.Id == episode.GeneratedBackgroundPictureId.Value && p.IsGeneratedBackground, cancellationToken);
            if (obsoletePicture is not null)
                _db.Pictures.Remove(obsoletePicture);
        }

        /// <summary>
        /// Marks the background image of the given episode as requiring an update and clears the cached entry.
        /// </summary>
        /// <param name="episodeId">The episode identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task MarkBackgroundImageForUpdateAsync(long episodeId, CancellationToken cancellationToken)
        {
            var episode = await _db.TVShowEpisodes.FirstOrDefaultAsync(e => e.Id == episodeId, cancellationToken);
            if (episode is null)
                return;

            episode.BackgroundImageRequiresUpdate = true;
            _cache.Remove(GetCacheKey(episodeId));
            await _db.SaveChangesAsync(cancellationToken);
        }

        private Task<long?> TryGetCachedImageIdAsync(long episodeId)
        {
            if (_cache.TryGetValue(GetCacheKey(episodeId), out long pictureId))
                return Task.FromResult<long?>(pictureId);

            return Task.FromResult<long?>(null);
        }

        private void CachePictureId(long episodeId, long pictureId)
        {
            _cache.Set(GetCacheKey(episodeId), pictureId, TimeSpan.FromMinutes(_options.CacheDurationMinutes));
        }

        private static string GetCacheKey(long episodeId) => $"EpisodeBackgroundImage_{episodeId}";
    }
}
