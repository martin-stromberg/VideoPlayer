using SQLite;
using VideoWebPlayer.Maui.Models;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Maui.Services.Events;

namespace VideoWebPlayer.Maui.Services;

/// <summary>
/// Download Manager - Phase 2: Vollständige SQLite-Integration
/// </summary>
public class DownloadManager
{
    private static DownloadManager? _instance;
    private SQLiteAsyncConnection _database => ClientDatabase.Instance.Database;
    private readonly string _downloadDirectory;
    private SemaphoreSlim _dbLock => ClientDatabase.Instance.Lock;
    private IPublishNotificationEvent? _eventPublisher;
    
    public static DownloadManager Instance => _instance ??= new DownloadManager();

    /// <summary>
    /// Sets the event publisher for download events.
    /// Should be called during app initialization.
    /// </summary>
    public void SetEventPublisher(IPublishNotificationEvent eventPublisher)
    {
        _eventPublisher = eventPublisher;
        System.Diagnostics.Debug.WriteLine("[DownloadManager] Event publisher configured");
    }
    
    private DownloadManager()
    {
        var profile = ProfileManager.Instance.CurrentProfile;
        _downloadDirectory = Path.Combine(FileSystem.AppDataDirectory, $"videos{(profile != "default" ? $"_{profile}" : "")}");
        Directory.CreateDirectory(_downloadDirectory);

        System.Diagnostics.Debug.WriteLine($"[DownloadManager] Initialized with profile '{profile}': Videos={_downloadDirectory}");

        // Führe Migration durch
        _ = Task.Run(async () => await MigrateAsync());

        // Registriere Cleanup-Task
        _ = Task.Run(async () => await SchedulePeriodicCleanupAsync());
    }

    internal static string NormalizeVideoType(string videoType)
    {
        if (string.IsNullOrWhiteSpace(videoType))
            return videoType;

        var t = videoType.Trim();

        // Legacy / UI aliases
        if (t.Equals("episode", StringComparison.OrdinalIgnoreCase))
            return MediaTypes.Episode;

        if (t.Equals("movie", StringComparison.OrdinalIgnoreCase) || t.Equals("Movie", StringComparison.OrdinalIgnoreCase))
            return MediaTypes.Movie;

        if (t.Equals(MediaTypes.Episode, StringComparison.OrdinalIgnoreCase))
            return MediaTypes.Episode;

        if (t.Equals(MediaTypes.Movie, StringComparison.OrdinalIgnoreCase))
            return MediaTypes.Movie;

        return t.ToLowerInvariant();
    }

    private async Task MigrateAsync()
    {
        try
        {
            // Überprüfe ob die Spalten existieren, falls nicht füge sie hinzu
            var tableInfo = await _database.GetTableInfoAsync(nameof(DownloadedVideo));
            
            var hasLocalPosterPath = tableInfo.Any(c => c.Name == nameof(DownloadedVideo.LocalPosterImagePath));
            var hasLocalBannerPath = tableInfo.Any(c => c.Name == nameof(DownloadedVideo.LocalBannerImagePath));
            
            if (!hasLocalPosterPath)
            {
                await _database.ExecuteAsync($"ALTER TABLE {nameof(DownloadedVideo)} ADD COLUMN {nameof(DownloadedVideo.LocalPosterImagePath)} TEXT");
                System.Diagnostics.Debug.WriteLine("[DownloadManager] Added LocalPosterImagePath column");
            }
            
            if (!hasLocalBannerPath)
            {
                await _database.ExecuteAsync($"ALTER TABLE {nameof(DownloadedVideo)} ADD COLUMN {nameof(DownloadedVideo.LocalBannerImagePath)} TEXT");
                System.Diagnostics.Debug.WriteLine("[DownloadManager] Added LocalBannerImagePath column");
            }

            // Normalisiere VideoType in bestehenden DBs (Legacy: "Movie"/"TVShowEpisode"/"episode")
            await _database.ExecuteAsync($"UPDATE {nameof(DownloadedVideo)} SET VideoType = lower(VideoType) WHERE VideoType IS NOT NULL");
            await _database.ExecuteAsync($"UPDATE {nameof(DownloadedVideo)} SET VideoType = '{MediaTypes.Episode}' WHERE lower(VideoType) = 'episode'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadManager] Migration error: {ex.Message}");
        }
    }
    
    public async Task<VideoRequest> RequestVideoAsync(long videoId, string videoType, string title)
    {
        var request = new VideoRequest
        {
            VideoId = videoId,
            VideoType = videoType,
            Title = title
        };
        
        // Starte Hintergrund-Task zur Quelle-Ermittlung
        _ = Task.Run(async () => await ResolveVideoSourceAsync(request));
        
        return request;
    }
    
    private async Task ResolveVideoSourceAsync(VideoRequest request)
    {
        try
        {
            // Prüfe ob lokale Datei vorhanden
            var download = await GetDownloadAsync(request.VideoId, request.VideoType);
            
            if (download != null && download.Status == DownloadStatus.Completed && File.Exists(download.LocalFilePath))
            {
                // Lokale Datei verfügbar mit gespeicherter Position
                request.SetSource(new VideoSourceInfo
                {
                    SourcePath = download.LocalFilePath,
                    SourceType = VideoSourceType.LocalFile,
                    ResumePosition = download.PlaybackPositionSeconds > 0 
                        ? TimeSpan.FromSeconds(download.PlaybackPositionSeconds) 
                        : null,
                    Duration = download.DurationSeconds > 0 
                        ? TimeSpan.FromSeconds(download.DurationSeconds) 
                        : null
                });
                return;
            }
            
            // Stream-URL vom Server - hole Position vom Server
            var streamUrl = BuildStreamUrl(request.VideoId, request.VideoType);
            var resumePosition = await FetchResumePositionFromServerAsync(request.VideoId, request.VideoType);
            
            request.SetSource(new VideoSourceInfo
            {
                SourcePath = streamUrl,
                SourceType = VideoSourceType.StreamUrl,
                ResumePosition = resumePosition
            });
            
            // Füge zur Download-Queue hinzu (Cache) - nur wenn noch nicht vorhanden
            if (download == null)
            {
                await QueueDownloadAsync(request, DownloadRetentionType.Cache);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error resolving video source: {ex.Message}");
        }
    }
    
    private async Task<TimeSpan?> FetchResumePositionFromServerAsync(long videoId, string videoType)
    {
        try
        {
            var client = App.ServiceProvider?.GetService<VideoWebPlayerClient>();
            if (client == null)
            {
                System.Diagnostics.Debug.WriteLine("VideoWebPlayerClient not available");
                return null;
            }
            
            // Hole Continue-Watching Liste
            var entries = await client.GetContinueWatchingAsync();
            if (entries == null || entries.Count == 0)
                return null;
            
            // Suche nach passendem Eintrag basierend auf MediaType und Entry.Id
            var entry = entries.FirstOrDefault(e => 
                e.MediaType.Equals(videoType, StringComparison.OrdinalIgnoreCase) && 
                e.Entry?.Id == videoId
            );
            
            if (entry != null && entry.PositionSeconds > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Found resume position from server: {entry.PositionSeconds}s for {videoType} {videoId}");
                return TimeSpan.FromSeconds(entry.PositionSeconds);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching resume position from server: {ex.Message}");
            return null;
        }
    }
    
    // Helper-Klasse für Deserialisierung
    private class ContinueWatchingEntry
    {
        public long? MovieId { get; set; }
        public long? EpisodeId { get; set; }
        public long PositionSeconds { get; set; }
        public long DurationSeconds { get; set; }
    }
    
    private string BuildStreamUrl(long videoId, string videoType)
    {
        videoType = NormalizeVideoType(videoType);

        var serverAddress = Preferences.Default.Get("ServerAddress", string.Empty);
        if (!serverAddress.StartsWith("http"))
        {
            serverAddress = $"http://{serverAddress}";
        }
        
        serverAddress = serverAddress.TrimEnd('/');
        
        // Hole Auth-Token
        var authToken = Preferences.Default.Get("AuthToken", string.Empty);
        
        string streamUrl;
        if (videoType.Equals(MediaTypes.Movie, StringComparison.OrdinalIgnoreCase))
        {
            streamUrl = $"{serverAddress}/api/items/{videoType}/{videoId}/stream";
        }
        else if (videoType.Equals(MediaTypes.Episode, StringComparison.OrdinalIgnoreCase))
        {
            streamUrl = $"{serverAddress}/api/items/{videoType}/{videoId}/stream";
        }
        else
        {
            throw new ArgumentException($"Unknown video type: {videoType}", nameof(videoType));
        }
        
        // Füge Auth-Token als Query-Parameter hinzu (falls vorhanden)
        if (!string.IsNullOrEmpty(authToken))
        {
            streamUrl += $"?access_token={Uri.EscapeDataString(authToken)}";
        }
        
        return streamUrl;
    }
    
    public async Task<DownloadedVideo?> GetDownloadAsync(long videoId, string videoType)
    {
        videoType = NormalizeVideoType(videoType);

        await _dbLock.WaitAsync();
        try
        {
            // Robust gegen Legacy-Daten ("Movie" vs "movie" etc.)
            var candidates = await _database.Table<DownloadedVideo>()
                .Where(d => d.VideoId == videoId)
                .ToListAsync();

            return candidates.FirstOrDefault(d =>
                string.Equals(NormalizeVideoType(d.VideoType), videoType, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    public async Task<List<DownloadedVideo>> GetAllDownloadsAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            return await _database.Table<DownloadedVideo>()
                .Where(d => d.Status == DownloadStatus.Completed)
                .OrderByDescending(d => d.DownloadedAt)
                .ToListAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    public async Task QueueDownloadAsync(VideoRequest request, DownloadRetentionType retentionType)
    {
        request.VideoType = NormalizeVideoType(request.VideoType);

        // Prüfe ob bereits in Datenbank vorhanden (completed Downloads)
        var existing = await GetDownloadAsync(request.VideoId, request.VideoType);
        if (existing != null && existing.Status == DownloadStatus.Completed)
        {
            if (File.Exists(existing.LocalFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"Download already exists: {request.Title}");
                return;
            }

            // DB-Eintrag ist completed, aber Datei fehlt -> Eintrag entfernen, damit neu geladen werden kann
            await _dbLock.WaitAsync();
            try
            {
                await _database.DeleteAsync(existing);
            }
            finally
            {
                _dbLock.Release();
            }
        }
        
        // Prüfe ob bereits in Queue (Arbeitsspeicher)
        if (DownloadQueue.Instance.IsInQueue(request.VideoId, request.VideoType))
        {
            System.Diagnostics.Debug.WriteLine($"Download already in queue: {request.Title}");
            return;
        }
        
        // Erstelle Download-Task (NUR im Arbeitsspeicher)
        var fileName = $"{request.VideoType}_{request.VideoId}_{DateTime.Now.Ticks}.mp4";
        var localPath = Path.Combine(_downloadDirectory, fileName);
        
        var settings = App.ServiceProvider?.GetService<ISettingsService>();
        var playbackCacheDays = settings?.PlaybackCacheRetentionDays ?? 1;
        var watchlistCacheDays = settings?.WatchlistCacheRetentionDays ?? 3;
        var downloadDays = settings?.DownloadRetentionDays ?? 7;

        var expiresAt = retentionType switch
        {
            DownloadRetentionType.Cache => DateTime.Now.AddDays(playbackCacheDays),
            DownloadRetentionType.Watchlist => DateTime.Now.AddDays(watchlistCacheDays),
            _ => DateTime.Now.AddDays(downloadDays)
        };
        
        var streamUrl = BuildStreamUrl(request.VideoId, request.VideoType);
        
        // Lade Metadaten vom Server
        var metadata = await FetchVideoMetadataAsync(request.VideoId, request.VideoType);
        
        var downloadTask = new DownloadTask
        {
            VideoId = request.VideoId,
            VideoType = NormalizeVideoType(request.VideoType),
            Title = request.Title,
            StreamUrl = streamUrl,
            LocalFilePath = localPath,
            RetentionType = retentionType,
            ExpiresAt = expiresAt,
            Plot = metadata?.Plot,
            GenreNames = metadata?.GenreNames,
            ReleaseYear = metadata?.ReleaseYear,
            EpisodeNumber = metadata?.EpisodeNumber,
            SeasonNumber = metadata?.SeasonNumber,
            TVShowName = metadata?.TVShowName,
            PosterImageUrl = metadata?.PosterImageUrl,
            BannerImageUrl = metadata?.BannerImageUrl
        };
        
        // Zur Queue hinzufügen (nur Arbeitsspeicher)
        DownloadQueue.Instance.EnqueueDownload(downloadTask);
    }
    
    private async Task<VideoMetadata?> FetchVideoMetadataAsync(long videoId, string videoType)
    {
        try
        {
            var client = App.ServiceProvider?.GetService<VideoWebPlayerClient>();
            if (client == null)
            {
                System.Diagnostics.Debug.WriteLine("VideoWebPlayerClient not available");
                return null;
            }
            
            var serverAddress = Preferences.Default.Get("ServerAddress", string.Empty);
            if (string.IsNullOrWhiteSpace(serverAddress))
                return null;
            
            if (!serverAddress.StartsWith("http"))
            {
                serverAddress = $"http://{serverAddress}";
            }
            serverAddress = serverAddress.TrimEnd('/');
            
            var metadata = new VideoMetadata();
            
            if (videoType.Equals(MediaTypes.Movie, StringComparison.OrdinalIgnoreCase))
            {
                // Lade Movie Details
                var movie = await client.RequestMovieAsync(videoId);
                if (movie != null)
                {
                    try
                    {
                        metadata.Plot = movie.Plot;
                        metadata.GenreNames = movie.GenreNames;
                        
                        if (movie.ReleaseDate != null)
                        {
                            metadata.ReleaseYear = movie.ReleaseDate?.Year.ToString();
                        }
                        
                        // Nutze Fanart > Banner > Poster für Poster (in dieser Reihenfolge)
                        if (movie.FanartPictureId.HasValue)
                        {
                            metadata.PosterImageUrl = $"{serverAddress}/api/pictures/{movie.FanartPictureId}";
                        }
                        else if (movie.BannerPictureId.HasValue)
                        {
                            metadata.BannerImageUrl = $"{serverAddress}/api/pictures/{movie.BannerPictureId}";
                            metadata.PosterImageUrl = $"{serverAddress}/api/pictures/{movie.BannerPictureId}";
                        }
                        else if (movie.PosterPictureId.HasValue)
                        {
                            metadata.PosterImageUrl = $"{serverAddress}/api/pictures/{movie.PosterPictureId}";
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error accessing movie properties: {ex.Message}");
                    }
                }
            }
            else if (videoType.Equals(MediaTypes.Episode, StringComparison.OrdinalIgnoreCase))
            {
                // Lade Episode Details (mit Fanart)
                var episode = await client.RequestTVShowEpisodeAsync(videoId);
                if (episode != null)
                {
                    try
                    {
                        metadata.Plot = episode.Plot;
                        metadata.EpisodeNumber = episode.Number;
                        
                        // Nutze Fanart für Episodes (nicht Poster)
                        if (episode.FanartPictureId.HasValue)
                        {
                            metadata.BannerImageUrl = $"{serverAddress}/api/pictures/{episode.FanartPictureId}";
                        }
                        else if (episode.PosterPictureId.HasValue)
                        {
                            metadata.BannerImageUrl = $"{serverAddress}/api/pictures/{episode.PosterPictureId}";
                        }
                        
                        // Versuche Season-Information zu laden
                        if (episode.Season != null)
                        {
                            metadata.SeasonNumber = episode.Season.Number;
                            if (episode.Season.Show != null)
                            {
                                metadata.TVShowName = episode.Season.Show.Name;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error accessing episode properties: {ex.Message}");
                    }
                }
            }
            
            return metadata;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching video metadata: {ex.Message}");
            return null;
        }
    }
    
    private class VideoMetadata
    {
        public string? Plot { get; set; }
        public string? GenreNames { get; set; }
        public string? ReleaseYear { get; set; }
        public int? EpisodeNumber { get; set; }
        public int? SeasonNumber { get; set; }
        public string? TVShowName { get; set; }
        public string? PosterImageUrl { get; set; }
        public string? BannerImageUrl { get; set; }
    }
    
    public async Task SaveCompletedDownloadAsync(DownloadTask task, long fileSizeBytes)
    {
        await _dbLock.WaitAsync();
        try
        {
            // Lade und speichere Bilder lokal
            string? localPosterPath = null;
            string? localBannerPath = null;
            
            var imagesDirectory = Path.Combine(FileSystem.AppDataDirectory, "images");
            Directory.CreateDirectory(imagesDirectory);
            
            if (!string.IsNullOrEmpty(task.PosterImageUrl))
            {
                localPosterPath = await DownloadImageAsync(task.PosterImageUrl, imagesDirectory, $"poster_{task.VideoType}_{task.VideoId}");
            }
            
            if (!string.IsNullOrEmpty(task.BannerImageUrl))
            {
                localBannerPath = await DownloadImageAsync(task.BannerImageUrl, imagesDirectory, $"banner_{task.VideoType}_{task.VideoId}");
            }
            
            var download = new DownloadedVideo
            {
                VideoId = task.VideoId,
                VideoType = task.VideoType,
                LocalFilePath = task.LocalFilePath,
                FileSizeBytes = fileSizeBytes,
                RetentionType = task.RetentionType,
                DownloadedAt = DateTime.Now,
                ExpiresAt = task.ExpiresAt,
                Title = task.Title,
                Status = DownloadStatus.Completed,
                ProgressPercent = 100,
                PlaybackPositionSeconds = 0,
                DurationSeconds = 0,
                Plot = task.Plot,
                GenreNames = task.GenreNames,
                ReleaseYear = task.ReleaseYear,
                EpisodeNumber = task.EpisodeNumber,
                SeasonNumber = task.SeasonNumber,
                TVShowName = task.TVShowName,
                LocalPosterImagePath = localPosterPath,
                LocalBannerImagePath = localBannerPath
            };
            
            await _database.InsertAsync(download);
            System.Diagnostics.Debug.WriteLine($"Saved completed download to database with metadata: {task.Title}");

            // Publish download completed event
            _eventPublisher?.Publish(new DownloadCompletedEvent(download));
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    private async Task<string?> DownloadImageAsync(string imageUrl, string directory, string baseFileName)
    {
        try
        {
            using var httpClient = new HttpClient();
            var token = Preferences.Default.Get("AuthToken", string.Empty);
            if (!string.IsNullOrEmpty(token))
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
            
            // Bestimme Dateiendung aus URL oder Content-Type
            var extension = Path.GetExtension(imageUrl);
            if (string.IsNullOrEmpty(extension) || extension.Contains('?'))
            {
                extension = ".jpg"; // Default
            }
            
            var fileName = $"{baseFileName}{extension}";
            var localPath = Path.Combine(directory, fileName);
            
            await File.WriteAllBytesAsync(localPath, imageBytes);
            
            System.Diagnostics.Debug.WriteLine($"Downloaded image to: {localPath}");
            return localPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error downloading image {imageUrl}: {ex.Message}");
            return null;
        }
    }
    
    public async Task UpdatePlaybackPositionAsync(long videoId, string videoType, double positionSeconds, double durationSeconds)
    {
        System.Diagnostics.Debug.WriteLine($"[DownloadManager] UpdatePlaybackPositionAsync called: {videoType} {videoId} - {positionSeconds}s / {durationSeconds}s");
        
        // Speichere Position lokal
        await _dbLock.WaitAsync();
        try
        {
            var download = await _database.Table<DownloadedVideo>()
                .Where(d => d.VideoId == videoId && d.VideoType == videoType)
                .FirstOrDefaultAsync();
            
            if (download != null)
            {
                download.PlaybackPositionSeconds = positionSeconds;
                download.DurationSeconds = durationSeconds;
                await _database.UpdateAsync(download);
                System.Diagnostics.Debug.WriteLine($"[DownloadManager] Updated playback position locally: {positionSeconds}s / {durationSeconds}s for {videoType} {videoId}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DownloadManager] Download not found in database for {videoType} {videoId}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadManager] Error updating local position: {ex.Message}");
        }
        finally
        {
            _dbLock.Release();
        }

        // Sende Position auch an Server (nur wenn VideoWebPlayerClient verfügbar ist)
        try
        {
            var client = App.ServiceProvider?.GetService<VideoWebPlayerClient>();
            if (client != null)
            {
                System.Diagnostics.Debug.WriteLine($"[DownloadManager] Sending position to server for {videoType} {videoId}");
                await client.ReportPlaybackProgressAsync(videoType, videoId, (long)positionSeconds, (long)durationSeconds);
                System.Diagnostics.Debug.WriteLine($"[DownloadManager] Successfully sent position to server");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DownloadManager] VideoWebPlayerClient not available");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadManager] Error reporting to server (non-blocking): {ex.Message}");
            // Nicht werfen - dies ist ein non-blocking Operation
        }
    }
    
    public async Task<double?> GetPlaybackPositionAsync(long videoId, string videoType)
    {
        var download = await GetDownloadAsync(videoId, videoType);
        return download?.PlaybackPositionSeconds;
    }
    
    /// <summary>
    /// Deletes a download and all associated files.
    /// </summary>
    /// <param name="videoId">The video ID.</param>
    /// <param name="videoType">The video type (movie or episode).</param>
    /// <returns>True if the download was deleted, false if it was not found.</returns>
    public async Task<bool> DeleteDownloadAsync(long videoId, string videoType)
    {
        videoType = NormalizeVideoType(videoType);

        await _dbLock.WaitAsync();
        try
        {
            var download = await _database.Table<DownloadedVideo>()
                .Where(d => d.VideoId == videoId)
                .ToListAsync();

            var match = download.FirstOrDefault(d =>
                string.Equals(NormalizeVideoType(d.VideoType), videoType, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                System.Diagnostics.Debug.WriteLine($"Download not found: {videoType} {videoId}");
                return false;
            }

            // Store info for event before deletion
            var title = match.Title;

            try
            {
                // Lösche Video-Datei
                if (File.Exists(match.LocalFilePath))
                {
                    File.Delete(match.LocalFilePath);
                    System.Diagnostics.Debug.WriteLine($"Deleted video file: {match.LocalFilePath}");
                }

                // Lösche Poster-Bild
                if (!string.IsNullOrEmpty(match.LocalPosterImagePath) && File.Exists(match.LocalPosterImagePath))
                {
                    File.Delete(match.LocalPosterImagePath);
                    System.Diagnostics.Debug.WriteLine($"Deleted poster image: {match.LocalPosterImagePath}");
                }

                // Lösche Banner-Bild
                if (!string.IsNullOrEmpty(match.LocalBannerImagePath) && File.Exists(match.LocalBannerImagePath))
                {
                    File.Delete(match.LocalBannerImagePath);
                    System.Diagnostics.Debug.WriteLine($"Deleted banner image: {match.LocalBannerImagePath}");
                }

                await _database.DeleteAsync(match);
                System.Diagnostics.Debug.WriteLine($"Deleted download from database: {title}");

                // Publish download deleted event
                _eventPublisher?.Publish(new DownloadDeletedEvent(videoId, videoType, title));

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting download: {ex.Message}");
                throw;
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    public async Task CleanupExpiredDownloadsAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            var expired = await _database.Table<DownloadedVideo>()
                .Where(d => d.ExpiresAt < DateTime.Now && d.Status == DownloadStatus.Completed)
                .ToListAsync();
            
            System.Diagnostics.Debug.WriteLine($"Cleaning up {expired.Count} expired downloads");
            
            foreach (var download in expired)
            {
                try
                {
                    // Store info for event before deletion
                    var videoId = download.VideoId;
                    var videoType = download.VideoType;
                    var title = download.Title;

                    // Lösche Video-Datei
                    if (File.Exists(download.LocalFilePath))
                    {
                        File.Delete(download.LocalFilePath);
                    }
                    
                    // Lösche Poster-Bild
                    if (!string.IsNullOrEmpty(download.LocalPosterImagePath) && File.Exists(download.LocalPosterImagePath))
                    {
                        File.Delete(download.LocalPosterImagePath);
                    }
                    
                    // Lösche Banner-Bild
                    if (!string.IsNullOrEmpty(download.LocalBannerImagePath) && File.Exists(download.LocalBannerImagePath))
                    {
                        File.Delete(download.LocalBannerImagePath);
                    }
                    
                    await _database.DeleteAsync(download);
                    System.Diagnostics.Debug.WriteLine($"Cleaned up expired download: {title}");

                    // Publish download deleted event
                    _eventPublisher?.Publish(new DownloadDeletedEvent(videoId, videoType, title));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting expired download: {ex.Message}");
                }
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    private async Task SchedulePeriodicCleanupAsync()
    {
        while (true)
        {
            try
            {
                // Führe Cleanup alle 6 Stunden aus
                await Task.Delay(TimeSpan.FromHours(6));
                await CleanupExpiredDownloadsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in periodic cleanup: {ex.Message}");
            }
        }
    }
}
