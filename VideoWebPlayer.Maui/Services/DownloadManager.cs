using SQLite;
using VideoWebPlayer.Maui.Models;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Maui.Services;

/// <summary>
/// Download Manager - Phase 2: Vollständige SQLite-Integration
/// </summary>
public class DownloadManager
{
    private static DownloadManager? _instance;
    private readonly SQLiteAsyncConnection _database;
    private readonly string _downloadDirectory;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    
    public static DownloadManager Instance => _instance ??= new DownloadManager();
    
    private DownloadManager()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "downloads.db3");
        _database = new SQLiteAsyncConnection(dbPath);
        _database.CreateTableAsync<DownloadedVideo>().Wait();
        
        _downloadDirectory = Path.Combine(FileSystem.AppDataDirectory, "videos");
        Directory.CreateDirectory(_downloadDirectory);
        
        // Registriere Cleanup-Task
        _ = Task.Run(async () => await SchedulePeriodicCleanupAsync());
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
        await _dbLock.WaitAsync();
        try
        {
            return await _database.Table<DownloadedVideo>()
                .Where(d => d.VideoId == videoId && d.VideoType == videoType)
                .FirstOrDefaultAsync();
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
        // Prüfe ob bereits in Datenbank vorhanden (completed Downloads)
        var existing = await GetDownloadAsync(request.VideoId, request.VideoType);
        if (existing != null && existing.Status == DownloadStatus.Completed)
        {
            System.Diagnostics.Debug.WriteLine($"Download already exists: {request.Title}");
            return;
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
        
        var expiresAt = retentionType == DownloadRetentionType.Cache 
            ? DateTime.Now.AddDays(1)
            : DateTime.Now.AddDays(7);
        
        var streamUrl = BuildStreamUrl(request.VideoId, request.VideoType);
        
        // Lade Metadaten vom Server
        var metadata = await FetchVideoMetadataAsync(request.VideoId, request.VideoType);
        
        var downloadTask = new DownloadTask
        {
            VideoId = request.VideoId,
            VideoType = request.VideoType,
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
                // Lade Movie Collection Details
                var movieCollection = await client.RequestMovieCollectionAsync(videoId) as dynamic;
                if (movieCollection != null)
                {
                    try
                    {
                        metadata.Plot = movieCollection.Plot;
                        metadata.GenreNames = movieCollection.GenreNames;
                        
                        if (movieCollection.ReleaseDate != null)
                        {
                            metadata.ReleaseYear = movieCollection.ReleaseDate?.Year.ToString();
                        }
                        
                        if (movieCollection.PosterPictureId != null)
                        {
                            metadata.PosterImageUrl = $"{serverAddress}/api/pictures/{movieCollection.PosterPictureId}";
                        }
                        if (movieCollection.BannerPictureId != null)
                        {
                            metadata.BannerImageUrl = $"{serverAddress}/api/pictures/{movieCollection.BannerPictureId}";
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error accessing movie collection properties: {ex.Message}");
                    }
                }
            }
            else if (videoType.Equals(MediaTypes.Episode, StringComparison.OrdinalIgnoreCase))
            {
                // Für Episode müssen wir die TV Show laden um an die Episode-Details zu kommen
                // Das ist komplexer, da wir erst die TVShow-ID benötigen
                // TODO: Implementiere Episode-Metadaten-Abruf
                System.Diagnostics.Debug.WriteLine("Episode metadata fetch not yet implemented");
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
                System.Diagnostics.Debug.WriteLine($"Updated playback position: {positionSeconds}s / {durationSeconds}s for {videoType} {videoId}");
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }
    
    public async Task<double?> GetPlaybackPositionAsync(long videoId, string videoType)
    {
        var download = await GetDownloadAsync(videoId, videoType);
        return download?.PlaybackPositionSeconds;
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
