using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VideoWebPlayer.Data;
using VideoWebPlayer.Events;
using VideoWebPlayer.Hubs;
using VideoWebPlayer.Services.EpisodeBackgroundImage;
using static System.Net.Mime.MediaTypeNames;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Verantwortlich f�r die Klassifizierung und Verarbeitung der gescannten MediaItems und MediaCollections.
    /// </summary>
    public class MediaSourceClassifier
    {
        private readonly ApplicationDbContext _db;
        private readonly SftpMediaSourceReader _sftpReader;
        private readonly RecentEntryService _recentEntryService;
        private readonly EventManager _eventManager;
        private readonly MediaUpdateNotificationService? _notificationService;
        private readonly EpisodeBackgroundImageService _episodeBackgroundImageService;
        private readonly ILogger<MediaSourceClassifier> _logger;

		private static int _classificationRunning;
		private static readonly object _classificationQueueLock = new();
		private static readonly Queue<long> _queuedCollectionTreeClassificationIds = new();
		private static readonly HashSet<long> _queuedCollectionTreeClassificationIdSet = new();

        private readonly string[] fileExtensions_Video = new string[] { ".mp4", ".avi", ".mkv", ".mpeg" };
        private static readonly string[] PictureTypes = new[] { "poster", "banner", "fanart", "thumb" };
        private static readonly string[] MovieCollectionPictureNames = new[] { "folder", "banner", "poster", "fanart" };
        private static readonly string[] ImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] TVShowPictureTypes = new[] { "poster", "banner", "fanart", "thumb", "" };
        private static readonly string[] TVShowImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceClassifier"/> class.
        /// </summary>
        /// <param name="db">Application database context.</param>
        /// <param name="sftpReader">SFTP reader for remote sources.</param>
        /// <param name="recentEntryService">Recent entry service.</param>
        /// <param name="logger">Logger instance.</param>
        public MediaSourceClassifier(
            ApplicationDbContext db,
            SftpMediaSourceReader sftpReader,
            RecentEntryService recentEntryService,
            EventManager eventManager,
            EpisodeBackgroundImageService episodeBackgroundImageService,
            ILogger<MediaSourceClassifier> logger,
            MediaUpdateNotificationService? notificationService = null)
        {
            _db = db;
            _sftpReader = sftpReader;
            _recentEntryService = recentEntryService;
            _eventManager = eventManager;
            _episodeBackgroundImageService = episodeBackgroundImageService;
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// F�hrt die Klassifizierung aller relevanten MediaItems und MediaCollections durch.
        /// </summary>
        public async Task ClassifyAllAsync(CancellationToken cancellationToken)
        {
			if (!TryBeginClassification())
			{
				_logger.LogInformation("Klassifizierung l�uft bereits, �berspringe ClassifyAllAsync.");
				return;
			}

			try
			{
            _logger.LogInformation("Starte Klassifizierung aller MediaItems und MediaCollections.");
            await ProcessMediaItemsAsync(cancellationToken);
            await ProcessMediaCollectionsAsync(cancellationToken);
            _logger.LogInformation("Klassifizierung abgeschlossen.");
			}
			finally
			{
				await FinishClassificationAsync(cancellationToken);
			}
        }

        /// <summary>
        /// F�hrt nur die Klassifizierung der relevanten MediaItems durch.
        /// </summary>
        public async Task ClassifyMediaItemsAsync(CancellationToken cancellationToken)
        {
			if (!TryBeginClassification())
			{
				_logger.LogInformation("Klassifizierung l�uft bereits, �berspringe ClassifyMediaItemsAsync.");
				return;
			}

			try
			{
            _logger.LogInformation("Starte Klassifizierung der MediaItems.");
            await ProcessMediaItemsAsync(cancellationToken);
            _logger.LogInformation("Klassifizierung der MediaItems abgeschlossen.");
			}
			finally
			{
				await FinishClassificationAsync(cancellationToken);
			}
        }

        /// <summary>
        /// F�hrt nur die Klassifizierung der relevanten MediaCollections durch.
        /// </summary>
        public async Task ClassifyMediaCollectionsAsync(CancellationToken cancellationToken)
        {
			if (!TryBeginClassification())
			{
				_logger.LogInformation("Klassifizierung l�uft bereits, �berspringe ClassifyMediaCollectionsAsync.");
				return;
			}

			try
			{
            _logger.LogInformation("Starte Klassifizierung der MediaCollections.");
            await ProcessMediaCollectionsAsync(cancellationToken);
            _logger.LogInformation("Klassifizierung der MediaCollections abgeschlossen.");
			}
			finally
			{
				await FinishClassificationAsync(cancellationToken);
			}
        }

        /// <summary>
        /// F�hrt die Klassifizierung f�r eine bestimmte Collection inkl. ihrer Unter-Collections durch.
        /// </summary>
        /// <param name="rootMediaCollectionId">Root-Collection (Startpunkt).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<bool> ClassifyCollectionTreeAsync(long rootMediaCollectionId, CancellationToken cancellationToken)
        {
			if (!TryBeginClassificationForCollectionTree(rootMediaCollectionId))
			{
				_logger.LogInformation("Klassifizierung l�uft bereits, CollectionId {CollectionId} wurde in Queue gepackt.", rootMediaCollectionId);
				return false;
			}

			try
			{
				await ClassifyCollectionTreeCoreAsync(rootMediaCollectionId, cancellationToken);
				return true;
			}
			finally
			{
				await FinishClassificationAsync(cancellationToken);
			}
        }

		private static bool TryBeginClassification()
			=> Interlocked.CompareExchange(ref _classificationRunning, 1, 0) == 0;

		private async Task FinishClassificationAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await DrainQueuedCollectionTreeClassificationsAsync(cancellationToken);

				lock (_classificationQueueLock)
				{
					if (_queuedCollectionTreeClassificationIds.Count == 0)
					{
						Interlocked.Exchange(ref _classificationRunning, 0);
						return;
					}
				}
			}

			Interlocked.Exchange(ref _classificationRunning, 0);
		}

		private static bool TryBeginClassificationForCollectionTree(long rootMediaCollectionId)
		{
			if (Interlocked.CompareExchange(ref _classificationRunning, 1, 0) == 0)
				return true;

			lock (_classificationQueueLock)
			{
				if (_queuedCollectionTreeClassificationIdSet.Add(rootMediaCollectionId))					
					_queuedCollectionTreeClassificationIds.Enqueue(rootMediaCollectionId);
			}

			return false;
		}

		private static bool TryDequeueQueuedCollectionTree(out long collectionId)
		{
			lock (_classificationQueueLock)
			{
				if (_queuedCollectionTreeClassificationIds.Count == 0)
				{
					collectionId = default;
					return false;
				}

				collectionId = _queuedCollectionTreeClassificationIds.Dequeue();
				_queuedCollectionTreeClassificationIdSet.Remove(collectionId);
				return true;
			}
		}

		private async Task DrainQueuedCollectionTreeClassificationsAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested && TryDequeueQueuedCollectionTree(out var nextId))
			{
				await ClassifyCollectionTreeCoreAsync(nextId, cancellationToken);
			}
		}

		private async Task ClassifyCollectionTreeCoreAsync(long rootMediaCollectionId, CancellationToken cancellationToken)
		{
			var collectionIds = await GetCollectionTreeIdsAsync(rootMediaCollectionId, cancellationToken);
			_logger.LogInformation("Starte Klassifizierung f�r Collection-Tree (Root={RootId}, Count={Count}).", rootMediaCollectionId, collectionIds.Count);

			await ProcessMediaItemsAsync(collectionIds, cancellationToken);
			await ProcessMediaCollectionsAsync(collectionIds, cancellationToken);

			_logger.LogInformation("Klassifizierung f�r Collection-Tree abgeschlossen (Root={RootId}).", rootMediaCollectionId);
		}

        private async Task<HashSet<long>> GetCollectionTreeIdsAsync(long rootMediaCollectionId, CancellationToken cancellationToken)
        {
            var visited = new HashSet<long>();
            var queue = new Queue<long>();
            queue.Enqueue(rootMediaCollectionId);

            while (queue.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var id = queue.Dequeue();
                if (!visited.Add(id))
                    continue;

                var childIds = await _db.MediaCollections
                    .Where(c => c.ParentMediaCollectionId == id)
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);

                foreach (var childId in childIds)
                    queue.Enqueue(childId);
            }

            return visited;
        }

        private void PublishStatus(string message)
        {
            _logger.LogInformation(message);
            _eventManager.Publish(new BackgroundProcessingStatusEvent(message, DateTimeOffset.UtcNow));
            _ = _notificationService?.NotifyStatusAsync(message);
        }

        private Task ProcessMediaItemsAsync(CancellationToken cancellationToken)
            => ProcessMediaItemsAsync(collectionIds: null, cancellationToken);

        private async Task ProcessMediaItemsAsync(IReadOnlyCollection<long>? collectionIds, CancellationToken cancellationToken)
        {
            var query = _db.MediaItems
                .Include(mi => mi.MediaCollection)
                .Where(mi => mi.MediaCollection.Classifyable && (mi.Changed || !mi.ClassifiedAt.HasValue));

            if (collectionIds != null)
            {
                query = query.Where(mi => collectionIds.Contains(mi.MediaCollectionId));
                _logger.LogInformation("Klassifiziere MediaItems (gefiltert).");
            }
            else
            {
                _logger.LogInformation("Klassifiziere MediaItems.");
            }

            var items = await query.ToListAsync(cancellationToken);

            var count = items.Count;
            foreach (var item in items)
            {
                count--;
                if (cancellationToken.IsCancellationRequested)
                    break;
                if (item.MediaCollection != null)
                    item.MediaCollection.Changed = true;
                item.Changed = false;
                item.ClassifiedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                if (count % 100 == 0)
                    PublishStatus($"Klassifizierung: {count} Dateien �brig.");
                await Task.Delay(10);
            }
        }

        private async Task ProcessMediaCollectionsAsync(CancellationToken cancellationToken)
        {
            List<MediaCollection>? collections = null;
            do
            {
                collections = _db.MediaCollections
                    .Include(mc => mc.MediaSource)
                    .Where(mc => mc.Classifyable && (mc.Changed || !mc.ClassifiedAt.HasValue))
                    .OrderByDescending(mc => mc.Id)
                    .ToList();

                _logger.LogInformation("Klassifiziere MediaCollections.");
                var count = collections.Count;
                foreach (var collection in collections)
                {
                    count--;
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (count % 100 == 0)
                        PublishStatus($"Klassifizierung: {count} Verzeichnisse �brig.");

                    _logger.LogInformation("Verarbeite Collection '{CollectionName}' (ID: {CollectionId})", collection.Name, collection.Id);

                    // 1. TVShow-Verarbeitung
                    await ProcessCollectionAsTVShowAsync(collection, cancellationToken);

                    // 2. Movie-Verarbeitung (falls TVShow nicht zutrifft oder zus�tzlich n�tig)
                    await ProcessCollectionAsMovieAsync(collection, cancellationToken);

                    collection.Changed = false;
                    collection.ClassifiedAt = DateTime.UtcNow;
                    if (collection.ParentMediaCollection != null)
                        collection.ParentMediaCollection.Changed = true;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            while (collections is not null && collections.Any());
        }

        private async Task ProcessMediaCollectionsAsync(IReadOnlyCollection<long> collectionIds, CancellationToken cancellationToken)
        {
            List<MediaCollection>? collections = null;
            do
            {
                collections = await _db.MediaCollections
                    .Include(mc => mc.MediaSource)
                    .Where(mc => collectionIds.Contains(mc.Id))
                    .Where(mc => mc.Classifyable && (mc.Changed || !mc.ClassifiedAt.HasValue))
                    .OrderByDescending(mc => mc.Id)
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Klassifiziere MediaCollections (gefiltert).");
                var count = collections.Count;
                foreach (var collection in collections)
                {
                    count--;
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (count % 100 == 0)
                        PublishStatus($"Klassifizierung: {count} Verzeichnisse �brig.");

                    _logger.LogInformation("Verarbeite Collection '{CollectionName}' (ID: {CollectionId})", collection.Name, collection.Id);

                    await ProcessCollectionAsTVShowAsync(collection, cancellationToken);
                    await ProcessCollectionAsMovieAsync(collection, cancellationToken);

                    collection.Changed = false;
                    collection.ClassifiedAt = DateTime.UtcNow;
                    if (collection.ParentMediaCollection != null)
                        collection.ParentMediaCollection.Changed = true;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            while (collections is not null && collections.Any());
        }

        /// <summary>
        /// Pr�ft und verarbeitet eine Collection als TVShow (z.B. wenn tvshow.nfo existiert).
        /// </summary>
        private async Task ProcessCollectionAsTVShowAsync(MediaCollection collection, CancellationToken cancellationToken)
        {
            bool hasTvShowNfo = await _sftpReader.FileExistsAsync(collection, "tvshow.nfo");
            if (!hasTvShowNfo)
            {
                _logger.LogDebug("Collection '{CollectionName}' hat keine tvshow.nfo.", collection.Name);
                return;
            }

            var nfoContent = await _sftpReader.ReadFileAsync(collection, "tvshow.nfo");
            if (string.IsNullOrWhiteSpace(nfoContent))
            {
                _logger.LogWarning("tvshow.nfo in Collection '{CollectionName}' ist leer.", collection.Name);
                return;
            }

            XElement? xml = null;
            try
            {
                xml = XElement.Parse(nfoContent);
            }
            catch
            {
                _logger.LogWarning("tvshow.nfo in Collection '{CollectionName}' ist kein g�ltiges XML.", collection.Name);
                return;
            }

            _logger.LogInformation("Verarbeite TVShow f�r Collection '{CollectionName}'.", collection.Name);
            var show = await CreateOrUpdateTVShow(collection, xml, cancellationToken);
            await ProcessEpisodesForTVShowAsync(show, cancellationToken);
        }

        private async Task<TVShow> CreateOrUpdateTVShow(MediaCollection collection, XElement xml, CancellationToken cancellationToken)
        {
            // Parse die relevanten Infos aus dem XML
            string showName = xml.Element("title")?.Value ?? collection.Name;

            // Prefer the stable source collection before falling back to the legacy title lookup.
            var existingShow = await _db.TVShows
                .Where(s => s.MediaSourceId == collection.MediaSourceId && s.CollectionId == collection.Id)
                .OrderByDescending(s => s.IsManuallyEdited)
                .ThenBy(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken)
                ?? await _db.TVShows
                    .FirstOrDefaultAsync(s => s.MediaSourceId == collection.MediaSourceId && s.Name == showName, cancellationToken);

            if (existingShow == null)
            {
                var tvShow = new TVShow
                {
                    Name = showName,
                    MediaSourceId = collection.MediaSourceId,
                    CollectionId = collection.Id,
                    CreatedAt = DateTime.UtcNow
                };
                tvShow.LoadFromXml(xml);
                _db.TVShows.Add(tvShow);
                existingShow = tvShow;
                await _db.SaveChangesAsync(cancellationToken);
                await _recentEntryService.AddTVShowAsync(tvShow).ConfigureAwait(false);
                PublishStatus($"Neue TVShow '{showName}' angelegt.");
            }
            else
            {
                if (!existingShow.IsManuallyEdited)
                {
                    existingShow.Name = showName;
                    existingShow.LoadFromXml(xml);
                }
                await _db.SaveChangesAsync(cancellationToken);
                PublishStatus($"TVShow '{showName}' aktualisiert.");
            }
            if (!existingShow.IsManuallyEdited)
            {
                var showGenres = await GetOrCreateGenresAsync(existingShow.GenreNames, collection.MediaSourceId, cancellationToken);
                existingShow.GenreNames = string.Join(",", showGenres.Select(g => g.Name));
                existingShow.TVShowGenres.Clear();
                foreach (var genre in showGenres)
                {
                    var existing = await _db.TVShowGenres.FirstOrDefaultAsync(mg => mg.TVShowId == existingShow.Id && mg.GenreId == genre.Id);
                    if (existing is not null)
                        existingShow.TVShowGenres.Add(existing);
                    else
                        existingShow.TVShowGenres.Add(new TVShowGenre { TVShowId = existingShow.Id, GenreId = genre.Id });
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            return existingShow;
        }

        // Dummy-Parser f�r ShowName aus NFO (bitte durch echtes XML-Parsing ersetzen)
        private string? ParseShowNameFromNfo(string nfoContent)
        {
            try
            {
                using var reader = new System.IO.StringReader(nfoContent);
                var xml = System.Xml.Linq.XDocument.Load(reader);
                var title = xml.Root?.Element("title")?.Value;
                return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
            }
            catch
            {
                return null;
            }
        }

        private async Task ProcessEpisodesForTVShowAsync(
            TVShow show,
            CancellationToken cancellationToken)
        {
            // Alle MediaItems aus der Collection und allen Unter-Collections laden
            var collectionIds = await _db.MediaCollections
                .Where(c => c.Id == show.CollectionId || c.ParentMediaCollectionId == show.CollectionId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var mediaItems = await _db.MediaItems
                .Where(mi => collectionIds.Contains(mi.MediaCollectionId))
                .Include(mi => mi.MediaCollection)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Verarbeite {Count} Dateien f�r TVShow '{ShowName}'.", mediaItems.Count, show.Name);

            var isFirst = true;

            foreach (var item in mediaItems)
            {
                var ext = Path.GetExtension(item.Path);
                if (!fileExtensions_Video.Contains(ext))
                    continue;

                // NFO-Dateiname bestimmen
                var nfoFileName = System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(item.Path), ".nfo");
                var collection = item.MediaCollection;

                // Pr�fen, ob NFO existiert
                bool nfoExists = await _sftpReader.FileExistsAsync(collection, nfoFileName);
                if (!nfoExists)
                    continue;

                // NFO laden
                var nfoContent = await _sftpReader.ReadFileAsync(collection, nfoFileName);
                if (string.IsNullOrWhiteSpace(nfoContent))
                    continue;

                // XML parsen
                XElement? xml = null;
                try { xml = XElement.Parse(nfoContent); }
                catch { continue; }

                // Staffelnummer und Episodennummer auslesen
                int seasonNo = int.TryParse(xml.Element("season")?.Value, out var s) ? s : 0;
                var seasonName = seasonNo == 0 ? "Specials" : $"Staffel {seasonNo.ToString().PadLeft(2, '0')}";
                int episodeNo = int.TryParse(xml.Element("episode")?.Value, out var e) ? e : 0;
                if (episodeNo == 0)
                    continue; // Keine Episode-Nummer, �berspringen

                // Prefer the stable season collection before falling back to the legacy season name lookup.
                var season = await _db.TVShowSeasons
                    .Where(se => se.TVShowId == show.Id && se.CollectionId == collection.Id)
                    .OrderByDescending(se => se.IsManuallyEdited)
                    .ThenBy(se => se.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? await _db.TVShowSeasons
                        .FirstOrDefaultAsync(se => se.TVShowId == show.Id && se.Name == $"{seasonName}", cancellationToken);
                if (season == null)
                {
                    season = new TVShowSeason
                    {
                        TVShowId = show.Id,
                        Name = seasonName,
                        CreatedAt = DateTime.UtcNow,
                        MediaSourceId = show.MediaSourceId,
                        CollectionId = collection.Id,
                    };
                    _db.TVShowSeasons.Add(season);
                    await _db.SaveChangesAsync(cancellationToken);
                    await _recentEntryService.AddTVShowSeasonAsync(season).ConfigureAwait(false);
                    PublishStatus($"Neue Staffel '{seasonName}' f�r TVShow '{show.Name}' angelegt.");
                }
                else
                {
                    season.CollectionId = collection.Id;
                    if (!season.IsManuallyEdited)
                        season.Name = seasonName;
                    await _db.SaveChangesAsync(cancellationToken);
                }

                // Episode suchen oder anlegen
                var episodeTitle = xml.Element("title")?.Value ?? item.Name;
                var existingEpisode = await _db.TVShowEpisodeMediaItems
                    .Include(link => link.TVShowEpisode)
                    .Where(link => link.MediaItemId == item.Id)
                    .Select(link => link.TVShowEpisode)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? await _db.TVShowEpisodes
                        .FirstOrDefaultAsync(ep =>
                            ep.TVShowSeasonId == season.Id &&
                            ep.Number == episodeNo, cancellationToken);

                if (existingEpisode == null)
                {
                    var episode = new TVShowEpisode
                    {
                        Name = episodeTitle,
                        Number = episodeNo,
                        TVShowSeasonId = season.Id,
                        MediaSourceId = show.MediaSourceId,
                        CreatedAt = DateTime.UtcNow,
                        ReleaseDate = DateTime.TryParse(xml.Element("aired")?.Value, out var aired) ? aired : (DateTime?)null,
                        PremieredAt = DateTime.TryParse(xml.Element("premiered")?.Value, out var prem) ? prem : (DateTime?)null,
                        Plot = xml.Element("plot")?.Value,
                        // Weitere Felder aus xml setzen
                    };
                    episode.EndedAt = episode.ReleaseDate > episode.PremieredAt ? episode.ReleaseDate : episode.PremieredAt;
                    _db.TVShowEpisodes.Add(episode);
                    existingEpisode = episode;
                    await _db.SaveChangesAsync(cancellationToken);
                    await _recentEntryService.AddTVShowEpisodeAsync(episode).ConfigureAwait(false);
                    PublishStatus($"Neue Episode '{episodeTitle}' (Staffel {seasonNo}, Episode {episodeNo}) angelegt.");
                }
                else
                {
                    if (!existingEpisode.IsManuallyEdited)
                    {
                        existingEpisode.Name = episodeTitle;
                        existingEpisode.ReleaseDate = DateTime.TryParse(xml.Element("aired")?.Value, out var aired) ? aired : (DateTime?)null;
                        existingEpisode.PremieredAt = DateTime.TryParse(xml.Element("premiered")?.Value, out var prem) ? prem : (DateTime?)null;
                        existingEpisode.EndedAt = existingEpisode.ReleaseDate > existingEpisode.PremieredAt ? existingEpisode.ReleaseDate : existingEpisode.PremieredAt;
                        existingEpisode.Plot = xml.Element("plot")?.Value;
                    }
                    await _db.SaveChangesAsync(cancellationToken);
                    PublishStatus($"Episode '{episodeTitle}' (Staffel {seasonNo}, Episode {episodeNo}) aktualisiert.");
                }                

                var episodeMediaItem = await _db.TVShowEpisodeMediaItems.FirstOrDefaultAsync(i => i.TVShowEpisodeId == existingEpisode.Id && i.MediaItemId == item.Id);
                if (episodeMediaItem is null)
                {
                    episodeMediaItem = new TVShowEpisodeMediaItem
                    {
                        TVShowEpisodeId = existingEpisode.Id,
                        MediaItemId = item.Id
                    };
                    _db.TVShowEpisodeMediaItems.Add(episodeMediaItem);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                if (existingEpisode.ReleaseDate.HasValue)
                {
                    if (!season.IsManuallyEdited)
                    {
                        season.ReleaseDate =
                            season.ReleaseDate.HasValue
                                ? (season.ReleaseDate < existingEpisode.ReleaseDate ? season.ReleaseDate : existingEpisode.ReleaseDate)
                                : existingEpisode.ReleaseDate;
                    }
                    if (!show.IsManuallyEdited)
                    {
                        show.ReleaseDate =
                            show.ReleaseDate.HasValue
                                ? (show.ReleaseDate < existingEpisode.ReleaseDate ? show.ReleaseDate : existingEpisode.ReleaseDate)
                                : existingEpisode.ReleaseDate;
                    }
                }
                if (existingEpisode.PremieredAt.HasValue)
                {
                    if (!season.IsManuallyEdited)
                    {
                        season.PremieredAt =
                            season.PremieredAt.HasValue
                                ? (season.PremieredAt < existingEpisode.PremieredAt ? season.PremieredAt : existingEpisode.PremieredAt)
                                : existingEpisode.PremieredAt;
                    }
                    if (!show.IsManuallyEdited)
                    {
                        show.PremieredAt =
                            show.PremieredAt.HasValue
                                ? (show.PremieredAt < existingEpisode.PremieredAt ? show.PremieredAt : existingEpisode.PremieredAt)
                                : existingEpisode.PremieredAt;
                    }
                }
                if (existingEpisode.EndedAt.HasValue)
                {
                    if (!season.IsManuallyEdited)
                    {
                        season.EndedAt =
                            season.EndedAt.HasValue
                                ? (season.EndedAt < existingEpisode.EndedAt ? season.EndedAt : existingEpisode.EndedAt)
                                : existingEpisode.EndedAt;
                    }
                    if (!show.IsManuallyEdited)
                    {
                        show.EndedAt =
                            show.EndedAt.HasValue
                                ? (show.EndedAt < existingEpisode.EndedAt ? show.EndedAt : existingEpisode.EndedAt)
                                : existingEpisode.EndedAt;
                    }
                }
                await _db.SaveChangesAsync(cancellationToken);
                await AssignPicturesToTVShowEpisodeAsync(existingEpisode, collection, item.Path, cancellationToken);
                await AssignPicturesToTVShowSeasonAsync(show, season, collection, cancellationToken, isFirst);
                await AssignPicturesToTVShowAsync(show, collection, cancellationToken, isFirst);
                isFirst = false;
            }
        }


        /// <summary>
        /// Pr�ft und verarbeitet eine Collection als Movie-Collection.
        /// </summary>
        private async Task ProcessCollectionAsMovieAsync(MediaCollection collection, CancellationToken cancellationToken)
        {
            // 1. Alle MediaItems (Videodateien) der Collection laden
            var mediaItems = await _db.MediaItems
                .Where(mi => mi.MediaCollectionId == collection.Id)
                .ToListAsync(cancellationToken);

            var movies = new List<Movie>();
            foreach (var item in mediaItems)
            {
                var ext = Path.GetExtension(item.Path);
                if (!fileExtensions_Video.Contains(ext))
                    continue;
                // NFO-Dateiname bestimmen
                var nfoFileName = System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(item.Path), ".nfo");

                // Pr�fen, ob NFO existiert
                bool nfoExists = await _sftpReader.FileExistsAsync(collection, nfoFileName);
                if (!nfoExists)
                    continue;

                // NFO laden
                var nfoContent = await _sftpReader.ReadFileAsync(collection, nfoFileName);
                if (string.IsNullOrWhiteSpace(nfoContent))
                    continue;

                // XML parsen
                XElement? xml = null;
                try { xml = XElement.Parse(nfoContent); }
                catch { continue; }

                // Pr�fen, ob es sich um einen Movie handelt
                if (!string.Equals(xml.Name.LocalName, "movie", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Movie-Name bestimmen
                var movieName = xml.Element("title")?.Value ?? item.Name;

                // Movie suchen oder anlegen
                var existingMovie = await _db.MovieMediaItems
                    .Include(mmi => mmi.Movie)
                    .Where(mmi => mmi.MediaItemId == item.Id)
                    .Select(mmi => mmi.Movie)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? await _db.Movies
                        .FirstOrDefaultAsync(m => m.CollectionId == collection.Id && m.Name == movieName, cancellationToken);

                if (existingMovie == null)
                {
                    var movie = new Movie
                    {
                        Name = movieName,
                        MediaSourceId = collection.MediaSourceId,
                        CollectionId = collection.Id,
                        CreatedAt = DateTime.UtcNow,
                        // ggf. weitere Initialwerte
                    };
                    movie.LoadFromXml(xml); // <-- XML-Daten setzen
                    _db.Movies.Add(movie);
                    await _db.SaveChangesAsync(cancellationToken);
                    existingMovie = movie;
                    movies.Add(movie);
                    await _recentEntryService.AddMovieAsync(movie).ConfigureAwait(false);
                    PublishStatus($"Neuer Film '{movieName}' angelegt.");
                }
                else
                {
                    if (!existingMovie.IsManuallyEdited)
                    {
                        existingMovie.Name = movieName;
                        existingMovie.LoadFromXml(xml); // <-- XML-Daten aktualisieren
                    }
                    await _db.SaveChangesAsync(cancellationToken);
                    movies.Add(existingMovie);
                    PublishStatus($"Film '{movieName}' aktualisiert.");
                }
                if (!existingMovie.IsManuallyEdited)
                {
                    var movieGenres = await GetOrCreateGenresAsync(existingMovie.GenreNames, collection.MediaSourceId, cancellationToken);
                    existingMovie.GenreNames = string.Join(",", movieGenres.Select(g => g.Name));
                    existingMovie.MovieGenres.Clear();
                    foreach (var genre in movieGenres)
                    {
                        var existing = await _db.MovieGenres.FirstOrDefaultAsync(mg => mg.MovieId == existingMovie.Id && mg.GenreId == genre.Id);
                        if (existing is not null)
                            existingMovie.MovieGenres.Add(existing);
                        else
                            existingMovie.MovieGenres.Add(new MovieGenre { MovieId = existingMovie.Id, GenreId = genre.Id });
                    }
                }
                await _db.SaveChangesAsync(cancellationToken);

                var movieMediaItem = await _db.MovieMediaItems
                    .FirstOrDefaultAsync(mmi => mmi.MovieId == existingMovie.Id && mmi.MediaItemId == item.Id, cancellationToken);
                if (movieMediaItem is null)
                {
                    movieMediaItem = new MovieMediaItem
                    {
                        MovieId = existingMovie.Id,
                        MediaItemId = item.Id
                    };
                    _db.MovieMediaItems.Add(movieMediaItem);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                await AssignPicturesToMovieAsync(existingMovie, collection, cancellationToken);
            }

            if (movies.Count > 0)
            {
                // 2. MovieCollection-Name bestimmen
                var collectionName = GetCommonPrefix(movies.Select(m => m.Name).ToList());
                if (string.IsNullOrWhiteSpace(collectionName) || collectionName.Length < 3)
                    collectionName = collection.Name;

                // 3. MovieCollection suchen oder anlegen
                var existingCollection = await _db.MovieCollections
                    .FirstOrDefaultAsync(mc => mc.MediaSourceId == collection.MediaSourceId && mc.CollectionId == collection.Id, cancellationToken);

                if (existingCollection == null)
                {
                    var movieCollection = new MovieCollection
                    {
                        Name = collectionName,
                        MediaSourceId = collection.MediaSourceId,
                        CollectionId = collection.Id,
                        CreatedAt = DateTime.UtcNow,
                        ReleaseDate = movies.Min(m => m.ReleaseDate),
                        PremieredAt = movies.Min(m => m.PremieredAt),
                        EndedAt = movies.Max(m => m.EndedAt)
                    }; 
                    _db.MovieCollections.Add(movieCollection);
                    await _db.SaveChangesAsync(cancellationToken);
                    existingCollection = movieCollection;
                    // Movies zuordnen
                    foreach (var movie in movies)
                    {
                        movie.MovieCollection = movieCollection;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                    await _recentEntryService.AddMovieCollectionAsync(movieCollection).ConfigureAwait(false);
                    PublishStatus($"Neue MovieCollection '{collectionName}' angelegt.");
                }
                else
                {
                    if (!existingCollection.IsManuallyEdited)
                    {
                        existingCollection.Name = collectionName;
                        existingCollection.ReleaseDate = movies.Min(m => m.ReleaseDate);
                        existingCollection.PremieredAt = movies.Min(m => m.PremieredAt);
                        existingCollection.EndedAt = movies.Max(m => m.EndedAt);
                    }
                    await _db.SaveChangesAsync(cancellationToken);
                    foreach (var movie in movies)
                    {
                        movie.MovieCollection = existingCollection;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                    PublishStatus($"MovieCollection '{collectionName}' aktualisiert.");
                }
                await AssignPicturesToMovieCollectionAsync(existingCollection, collection, cancellationToken);
            }
        }

        private async Task AssignPicturesToMovieAsync(Movie movie, MediaCollection collection, CancellationToken cancellationToken)
        {
            // Alle zugeordneten MediaItems des Movies laden
            var movieMediaItems = await _db.MovieMediaItems
                .Include(mmi => mmi.MediaItem)
                .Where(mmi => mmi.MovieId == movie.Id)
                .ToListAsync(cancellationToken);

            if (movieMediaItems.Count == 0)
                return;

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var images = await _db.MediaItems
                .Where(mi => mi.MediaCollectionId == collection.Id)
                .ToListAsync(cancellationToken);
            images = images.Where(mi => imageExtensions.Contains(Path.GetExtension(mi.Path).ToLower()))
                .ToList();

            foreach (var mmi in movieMediaItems)
            {
                var movieBaseName = Path.GetFileNameWithoutExtension(mmi.MediaItem.Path).ToLower();

                foreach (var img in images)
                {
                    var fileName = Path.GetFileNameWithoutExtension(img.Path).ToLower();
                    foreach (var type in PictureTypes)
                    {
                        if (fileName.StartsWith(movieBaseName + "-" + type))
                        {
                            Picture? picture = await GetOrCreatePicture(img, type, cancellationToken);
                            if (type == "poster")
                                movie.PosterPictureId = picture.Id;
                            else if (type == "banner")
                                movie.BannerPictureId = picture.Id;
                            else if (type == "fanart")
                                movie.FanartPictureId = picture.Id;
                            await _db.SaveChangesAsync(cancellationToken);
                        }
                    }
                }
            }
        }

        private async Task<Picture?> GetOrCreatePicture(MediaItem img, string type, CancellationToken cancellationToken)
        {
            var ext = Path.GetExtension(img.Path);
            if (img.MediaCollection is null)
            {
                img.MediaCollection = await _db.MediaCollections
                    .FirstOrDefaultAsync(mc => mc.Id == img.MediaCollectionId, cancellationToken);
            }
            var picture = await _db.Pictures.FirstOrDefaultAsync(p => p.MediaItemId == img.Id && p.Type == type, cancellationToken);
            if (picture == null)
            {
                picture = new Picture
                {
                    MediaItemId = img.Id,
                    Type = type,
                    Description = img.Name,
                    ContentType = $"image/{ext}",
                    Data = new byte[0]
                };
                _db.Pictures.Add(picture);
                await _db.SaveChangesAsync(cancellationToken);
            }
            if ((picture.Data is null || picture.Data.Length == 0) && img.MediaCollection is not null)
            {
                var imageBytes = await _sftpReader.ReadFileStreamAsync(img.MediaCollection, Path.GetFileName(img.Path));
                if (imageBytes is not null)
                {
                    picture.Data = ConvertStreamToByteArray(imageBytes);
                    picture.ContentType = $"image/{ext}";
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            if (string.IsNullOrWhiteSpace(picture.ContentType))
            {
                picture.ContentType = $"image/{ext}";
                await _db.SaveChangesAsync(cancellationToken);
            }
            return picture;
        }

        private async Task AssignPicturesToMovieCollectionAsync(MovieCollection movieCollection, MediaCollection collection, CancellationToken cancellationToken)
        {
            foreach (var baseName in MovieCollectionPictureNames)
            {
                foreach (var ext in ImageExtensions)
                {
                    var fileName = $"{baseName}{ext}";
                    bool exists = await _sftpReader.FileExistsAsync(collection, fileName);
                    if (exists)
                    {
                        // Pr�fe, ob das Bild bereits als MediaItem existiert
                        var mediaItem = (await _db.MediaItems
                            .Where(mi => mi.MediaCollectionId == collection.Id && mi.Path.EndsWith(fileName))
                            .ToListAsync(cancellationToken)
                            ).FirstOrDefault(mi => Path.GetFileName(mi.Path) == fileName);

                        if (mediaItem == null)
                        {
                            // Optional: MediaItem anlegen, falls gew�nscht
                            continue;
                        }

                        Picture? picture = await GetOrCreatePicture(mediaItem, baseName, cancellationToken);
                        // Weisen das Bild der MovieCollection zu
                        switch (baseName)
                        {
                            case "poster":
                                movieCollection.PosterPictureId = picture.Id;
                                break;
                            case "banner":
                                movieCollection.BannerPictureId = picture.Id;
                                break;
                            case "fanart":
                                movieCollection.FanartPictureId = picture.Id;
                                break;
                            case "folder":
                                if (movieCollection.PosterPictureId is null)
                                    movieCollection.PosterPictureId = picture.Id;
                                break;
                        }
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            // Falls kein explizites Poster oder Banner gefunden wurde, nimm das erste aus den Filmen
            if (movieCollection.PosterPictureId == null || movieCollection.BannerPictureId == null || movieCollection.FanartPictureId == null)
            {
                var firstMovieWithPoster = await _db.Movies
                    .Where(m => m.MovieCollectionId == movieCollection.Id && m.PosterPictureId != null)
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (movieCollection.PosterPictureId == null && firstMovieWithPoster?.PosterPictureId != null)
                    movieCollection.PosterPictureId = firstMovieWithPoster.PosterPictureId;
                await _db.SaveChangesAsync(cancellationToken);

                var firstMovieWithBanner = await _db.Movies
                    .Where(m => m.MovieCollectionId == movieCollection.Id && m.BannerPictureId != null)
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (movieCollection.BannerPictureId == null && firstMovieWithBanner?.BannerPictureId != null)
                    movieCollection.BannerPictureId = firstMovieWithBanner.BannerPictureId;
                await _db.SaveChangesAsync(cancellationToken);

                var firstMovieWithFanart= await _db.Movies
                    .Where(m => m.MovieCollectionId == movieCollection.Id && m.FanartPictureId != null)
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (movieCollection.FanartPictureId == null && firstMovieWithFanart?.FanartPictureId != null)
                    movieCollection.FanartPictureId = firstMovieWithFanart.FanartPictureId;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task AssignPicturesToTVShowAsync(TVShow show, MediaCollection episodeCollection, CancellationToken cancellationToken, bool isFirst)
        {
            if (!isFirst && (show.PosterPictureId ?? 0) != 0 && (show.BannerPictureId ?? 0) != 0)
                return;
            // 1. TVShow-Bilder direkt in der Collection suchen
            var pictureIds = new Dictionary<string, long?>();
            var collection = episodeCollection;
            while (collection is not null)
            {
                if (collection.Id == show.CollectionId)
                    foreach (var type in TVShowPictureTypes)
                    {
                        foreach (var ext in TVShowImageExtensions)
                        {
                            var fileName = $"{type}{ext}";
                            bool exists = await _sftpReader.FileExistsAsync(collection, fileName);
                            if (exists)
                            {
                                var mediaItem = await _db.MediaItems
                                    .FirstOrDefaultAsync(mi => mi.MediaCollectionId == collection.Id && mi.Path.EndsWith(fileName), cancellationToken);

                                if (mediaItem == null)
                                    continue;

                                Picture? picture = await GetOrCreatePicture(mediaItem, type, cancellationToken);                                

                                pictureIds[type] = picture.Id;
                            }
                        }
                    }
                collection = collection.ParentMediaCollection;
            }

            // 2. Fallback: Von Staffel �bernehmen, falls kein Bild gefunden
            if (!pictureIds.ContainsKey("poster") || !pictureIds.ContainsKey("banner") || !pictureIds.ContainsKey("fanart"))
            {
                var firstSeasonWithBanner = await _db.TVShowSeasons
                    .Where(s => s.TVShowId == show.Id && s.BannerPictureId != null)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstSeasonWithBanner != null)
                {
                    if (!pictureIds.ContainsKey("banner") && firstSeasonWithBanner.BannerPictureId != null)
                        pictureIds["banner"] = firstSeasonWithBanner.BannerPictureId;
                }

                var firstSeasonWithPoster = await _db.TVShowSeasons
                    .Where(s => s.TVShowId == show.Id && s.PosterPictureId != null)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstSeasonWithPoster != null)
                {
                    if (!pictureIds.ContainsKey("poster") && firstSeasonWithPoster.PosterPictureId != null)
                        pictureIds["poster"] = firstSeasonWithPoster.PosterPictureId;                    
                }

                var firstSeasonWithFanart = await _db.TVShowSeasons
                    .Where(s => s.TVShowId == show.Id && s.FanartPictureId != null)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstSeasonWithFanart != null)
                {
                    if (!pictureIds.ContainsKey("fanart") && firstSeasonWithFanart.FanartPictureId != null)
                        pictureIds["fanart"] = firstSeasonWithFanart.FanartPictureId;
                }
            }

            show.PosterPictureId = pictureIds.ContainsKey("poster") ? pictureIds["poster"] : null;
            show.BannerPictureId = pictureIds.ContainsKey("banner") ? pictureIds["banner"] : null;
            show.FanartPictureId = pictureIds.ContainsKey("fanart") ? pictureIds["fanart"] : null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task AssignPicturesToTVShowSeasonAsync(TVShow show, TVShowSeason season, MediaCollection tvShowCollection, CancellationToken cancellationToken, bool isFirst)
        {
            if (!isFirst && (season.PosterPictureId ?? 0) != 0 && (season.BannerPictureId ?? 0) != 0)
                return;

            // 1. Staffelbilder in der TVShow-Collection suchen (z.B. season02-banner.jpg)
            var seasonNumber = season.Name?.ToLower().Replace("staffel", "").Trim().PadLeft(2, '0') ?? "01";
            var pictureIds = new Dictionary<string, long?>();
            var collection = tvShowCollection;
            while (collection is not null)
            {
                if (collection.Id == show.CollectionId || collection.Id == season.CollectionId)
                    foreach (var type in TVShowPictureTypes)
                    {
                        foreach (var ext in TVShowImageExtensions)
                        {
                            var fileName = $"season{seasonNumber}-{type}{ext}";
                            bool exists = await _sftpReader.FileExistsAsync(collection, fileName);
                            if (exists)
                            {
                                var mediaItem = await _db.MediaItems
                                    .FirstOrDefaultAsync(mi => mi.MediaCollectionId == collection.Id && mi.Path.EndsWith(fileName), cancellationToken);

                                if (mediaItem == null)
                                    continue;

                                Picture? picture = await GetOrCreatePicture(mediaItem, type, cancellationToken);                                

                                pictureIds[type] = picture.Id;
                            }
                        }
                    }
                collection = collection.ParentMediaCollection;
            }

            // 2. Fallback: Von Episode �bernehmen, falls kein Bild gefunden
            if (!pictureIds.ContainsKey("poster") || !pictureIds.ContainsKey("banner") || !pictureIds.ContainsKey("fanart"))
            {
                var firstEpisodeWithBanner = await _db.TVShowEpisodes
                    .Where(e => e.TVShowSeasonId == season.Id && e.BannerPictureId != null)
                    .OrderBy(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstEpisodeWithBanner != null)
                {
                    if (!pictureIds.ContainsKey("banner") && firstEpisodeWithBanner.BannerPictureId != null)
                        pictureIds["banner"] = firstEpisodeWithBanner.BannerPictureId;
                }

                var firstEpisodeWithPoster = await _db.TVShowEpisodes
                    .Where(e => e.TVShowSeasonId == season.Id && e.PosterPictureId != null)
                    .OrderBy(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstEpisodeWithPoster != null)
                {
                    if (!pictureIds.ContainsKey("poster") && firstEpisodeWithPoster.PosterPictureId != null)
                        pictureIds["poster"] = firstEpisodeWithPoster.PosterPictureId;                    
                }

                var firstEpisodeWithFanart = await _db.TVShowEpisodes
                    .Where(e => e.TVShowSeasonId == season.Id && e.FanartPictureId != null)
                    .OrderBy(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (firstEpisodeWithFanart != null)
                {
                    if (!pictureIds.ContainsKey("fanart") && firstEpisodeWithFanart.FanartPictureId != null)
                        pictureIds["fanart"] = firstEpisodeWithFanart.FanartPictureId;
                }
            }

            season.PosterPictureId = pictureIds.ContainsKey("poster") ? pictureIds["poster"] : null;
            season.BannerPictureId = pictureIds.ContainsKey("banner") ? pictureIds["banner"] : null;
            season.FanartPictureId = pictureIds.ContainsKey("fanart") ? pictureIds["fanart"] : null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task AssignPicturesToTVShowEpisodeAsync(TVShowEpisode episode, MediaCollection collection, string videoFileName, CancellationToken cancellationToken)
        {
            // 1. Episodenbild ergibt sich aus dem Dateinamen der Videodatei (z.B. Folge01-thumb.jpg)
            foreach (var type in TVShowPictureTypes)
            {
                var postfix = $"-{type}";
                if (postfix == "-") postfix = "";
                foreach (var ext in TVShowImageExtensions)
                {
                    var baseName = System.IO.Path.GetFileNameWithoutExtension(videoFileName);
                    var fileName = $"{baseName}{postfix}{ext}";
                    bool exists = await _sftpReader.FileExistsAsync(collection, fileName);
                    if (exists)
                    {
                        var mediaItem = await _db.MediaItems
                            .FirstOrDefaultAsync(mi => mi.MediaCollectionId == collection.Id && mi.Path.EndsWith(fileName), cancellationToken);

                        if (mediaItem == null)
                            continue;

                        Picture? picture = await GetOrCreatePicture(mediaItem, type, cancellationToken);

                        if (type == "poster")
                        {
                            await SetPictureAndMarkBackgroundForUpdateAsync(episode, picture, (e, p) => e.PosterPictureId = p.Id, cancellationToken);
                        }
                        if (type == "banner")
                            episode.BannerPictureId = picture.Id;
                        if (type == "fanart")
                        {
                            await SetPictureAndMarkBackgroundForUpdateAsync(episode, picture, (e, p) => e.FanartPictureId = p.Id, cancellationToken);
                        }
                        if (type == "thumb")
                            if (episode.PosterPictureId is null)
                            {
                                await SetPictureAndMarkBackgroundForUpdateAsync(episode, picture, (e, p) => e.PosterPictureId = p.Id, cancellationToken);
                            }
                        if (type == "")
                            if (episode.PosterPictureId is null)
                            {
                                await SetPictureAndMarkBackgroundForUpdateAsync(episode, picture, (e, p) => e.PosterPictureId = p.Id, cancellationToken);
                            }
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }

        private async Task SetPictureAndMarkBackgroundForUpdateAsync(TVShowEpisode episode, Picture picture, Action<TVShowEpisode, Picture> assign, CancellationToken cancellationToken)
        {
            assign(episode, picture);
            await _episodeBackgroundImageService.MarkBackgroundImageForUpdateAsync(episode.Id, cancellationToken);
        }

        /// <summary>
        /// Gibt das l�ngste gemeinsame Pr�fix aller Strings in der Liste zur�ck.
        /// </summary>
        private static string GetCommonPrefix(List<string> strings)
        {
            if (strings == null || strings.Count == 0)
                return string.Empty;

            string prefix = strings[0];
            for (int i = 1; i < strings.Count; i++)
            {
                int j = 0;
                while (j < prefix.Length && j < strings[i].Length && prefix[j] == strings[i][j])
                {
                    j++;
                }
                prefix = prefix.Substring(0, j);
                if (string.IsNullOrEmpty(prefix))
                    break;
            }
            return prefix.Trim();
        }

        private static byte[] ConvertStreamToByteArray(Stream stream)
        {
            if (stream is MemoryStream ms)
                return ms.ToArray();
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Rebuilds genre mappings for movies and TV shows.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task ReloadGenres(CancellationToken cancellationToken)
        {
            // Filme ohne GenreNames korrigieren
            var movies = await _db.Movies
                .Where(m => !m.IsManuallyEdited && !string.IsNullOrWhiteSpace(m.GenreNames))
                .ToListAsync(cancellationToken);

            foreach (var movie in movies)
            {
                // Genre-Datens�tze anlegen und zuordnen
                var genres = await GetOrCreateGenresAsync(movie.GenreNames, movie.MediaSourceId, cancellationToken);
                movie.MovieGenres.Clear();
                foreach (var genre in genres)
                {
                    var existing = await _db.MovieGenres.FirstOrDefaultAsync(mg => mg.MovieId == movie.Id && mg.GenreId == genre.Id);
                    if (existing is not null)
                        movie.MovieGenres.Add(existing);
                    else
                        movie.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genre.Id });
                }
                movie.GenreNames = string.Join(",", genres.Select(g => g.Name));
                await _db.SaveChangesAsync(cancellationToken);
            }

            // TVShows ohne GenreNames korrigieren
            var tvshows = await _db.TVShows
                .Where(s => !s.IsManuallyEdited && !string.IsNullOrWhiteSpace(s.GenreNames))
                .ToListAsync(cancellationToken);

            foreach (var show in tvshows)
            {
                // Genre-Datens�tze anlegen und zuordnen
                var genres = await GetOrCreateGenresAsync(show.GenreNames, show.MediaSourceId, cancellationToken);
                show.GenreNames = string.Join(",", genres.Select(g => g.Name));
                show.TVShowGenres.Clear();
                foreach (var genre in genres)
                {
                    var existing = await _db.TVShowGenres.FirstOrDefaultAsync(mg => mg.TVShowId == show.Id && mg.GenreId == genre.Id);
                    if (existing is not null)
                        show.TVShowGenres.Add(existing);
                    else
                        show.TVShowGenres.Add(new TVShowGenre { TVShowId = show.Id, GenreId = genre.Id });
                }                
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        // Hilfsmethode wie oben beschrieben
        private async Task<List<Genre>> GetOrCreateGenresAsync(string? genreString, long mediaSourceId, CancellationToken cancellationToken)
        {
            var resultGenres = new List<Genre>();
            if (string.IsNullOrWhiteSpace(genreString))
                return resultGenres;

            var genreNames = genreString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(g => g.Trim())
                                        .Where(g => !string.IsNullOrWhiteSpace(g))
                                        .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var name in genreNames)
            {
                // 1. Suche Genre mit gleichem Namen in dieser Quelle
                var genre = (await _db.Genres
                    .Include(g => g.AlternateNames)
                    .Where(g => g.MediaSourceId == mediaSourceId)
                    .ToListAsync(cancellationToken))
                    .FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                // 2. Falls nicht gefunden, suche nach Alternativnamen
                if (genre == null)
                {
                    var newGenre = new Genre { Name = name, MediaSourceId = mediaSourceId };
                    _db.Genres.Add(newGenre);
                    await _db.SaveChangesAsync(cancellationToken);
                    resultGenres.Add(newGenre);
                }
                else
                {
                    var alternateNames = string.Join(", ", await _db.GenreNames.Where(a => a.GenreId == genre.Id && a.Name != genre.Name).Select(a => a.Name).ToListAsync());
                    if (string.IsNullOrWhiteSpace(alternateNames))
                    {
                        resultGenres.Add(genre);
                        continue;
                    }
                    resultGenres.AddRange(await GetOrCreateGenresAsync(alternateNames, mediaSourceId, cancellationToken));
                    continue;
                }
            }

            // Duplikate entfernen (z.B. falls mehrere Alternativen auf dasselbe Genre zeigen)
            return resultGenres
                .GroupBy(g => g.Id)
                .Select(g => g.First())
                .ToList();
        }

        internal async Task CheckReloadGenres(CancellationToken stoppingToken)
        {
            var setup = await _db.Setups.FirstOrDefaultAsync();
            if (setup is null || !setup.GenresChanged)
                return;
            setup.GenresChanged = false;
            await _db.SaveChangesAsync();
            try
            {
                await (ReloadGenres(stoppingToken));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Reload der Genres.");
                setup = await _db.Setups.FirstOrDefaultAsync();
                setup.GenresChanged = true;
                await _db.SaveChangesAsync();
            }            
        }
    }
}
