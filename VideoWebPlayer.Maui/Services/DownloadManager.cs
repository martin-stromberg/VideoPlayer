using SQLite;
using VideoWebPlayer.Maui.Models;

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
                // Lokale Datei verfügbar
                request.SetSource(new VideoSourceInfo
                {
                    SourcePath = download.LocalFilePath,
                    SourceType = VideoSourceType.LocalFile
                });
                return;
            }
            
            // Stream-URL vom Server
            var streamUrl = BuildStreamUrl(request.VideoId, request.VideoType);
            
            request.SetSource(new VideoSourceInfo
            {
                SourcePath = streamUrl,
                SourceType = VideoSourceType.StreamUrl
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
        if (videoType == "Movie")
        {
            streamUrl = $"{serverAddress}/api/items/movie/{videoId}/stream";
        }
        else // Episode
        {
            streamUrl = $"{serverAddress}/api/items/episode/{videoId}/stream";
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
        var downloadTask = new DownloadTask
        {
            VideoId = request.VideoId,
            VideoType = request.VideoType,
            Title = request.Title,
            StreamUrl = streamUrl,
            LocalFilePath = localPath,
            RetentionType = retentionType,
            ExpiresAt = expiresAt
        };
        
        // Zur Queue hinzufügen (nur Arbeitsspeicher)
        DownloadQueue.Instance.EnqueueDownload(downloadTask);
    }
    
    public async Task SaveCompletedDownloadAsync(DownloadTask task, long fileSizeBytes)
    {
        await _dbLock.WaitAsync();
        try
        {
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
                ProgressPercent = 100
            };
            
            await _database.InsertAsync(download);
            System.Diagnostics.Debug.WriteLine($"Saved completed download to database: {task.Title}");
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
                    if (File.Exists(download.LocalFilePath))
                    {
                        File.Delete(download.LocalFilePath);
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
