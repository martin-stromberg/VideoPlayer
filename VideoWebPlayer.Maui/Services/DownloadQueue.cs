using System.Collections.Concurrent;
using VideoWebPlayer.Maui.Models;

namespace VideoWebPlayer.Maui.Services;

public class DownloadQueue
{
    private static DownloadQueue? _instance;
    private readonly ConcurrentQueue<DownloadTask> _queue = new();
    private readonly HashSet<string> _queuedItems = new(); // Track items in queue
    private readonly object _queueLock = new();
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isProcessing = false;
    
    public static DownloadQueue Instance => _instance ??= new DownloadQueue();
    
    public event EventHandler<DownloadProgressEventArgs>? DownloadProgress;
    public event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;
    
    private DownloadQueue()
    {
        // Starte Background-Prozessing
        _ = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
    }
    
    public void EnqueueDownload(DownloadTask task)
    {
        var key = $"{task.VideoType}_{task.VideoId}";
        
        lock (_queueLock)
        {
            if (_queuedItems.Contains(key))
            {
                System.Diagnostics.Debug.WriteLine($"Download already in queue: {task.Title}");
                return;
            }
            
            _queue.Enqueue(task);
            _queuedItems.Add(key);
        }
        
        System.Diagnostics.Debug.WriteLine($"Download queued: {task.Title} (Queue size: {_queue.Count})");
    }
    
    public bool IsInQueue(long videoId, string videoType)
    {
        var key = $"{videoType}_{videoId}";
        lock (_queueLock)
        {
            return _queuedItems.Contains(key);
        }
    }
    
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryDequeue(out var task))
                {
                    await ProcessDownloadAsync(task, cancellationToken);
                }
                else
                {
                    // Warte 1 Sekunde wenn Queue leer ist
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in download queue: {ex.Message}");
            }
        }
    }
    
    private async Task ProcessDownloadAsync(DownloadTask task, CancellationToken cancellationToken)
    {
        System.Diagnostics.Debug.WriteLine($"Starting download: {task.Title}");
        
        var key = $"{task.VideoType}_{task.VideoId}";
        
        try
        {
            // Download-Logik
            using var httpClient = new HttpClient();
            var token = Preferences.Default.Get("AuthToken", string.Empty);
            if (!string.IsNullOrEmpty(token))
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            httpClient.Timeout = TimeSpan.FromMinutes(30);
            
            using var response = await httpClient.GetAsync(task.StreamUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;
            
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(task.LocalFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            
            var buffer = new byte[8192];
            int bytesRead;
            
            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;
                
                // Progress Report
                if (totalBytes > 0)
                {
                    var progress = (double)downloadedBytes / totalBytes * 100;
                    DownloadProgress?.Invoke(this, new DownloadProgressEventArgs
                    {
                        VideoId = task.VideoId,
                        VideoType = task.VideoType,
                        ProgressPercent = progress,
                        DownloadedBytes = downloadedBytes,
                        TotalBytes = totalBytes
                    });
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Download completed: {task.Title}");
            
            // JETZT erst in Datenbank speichern (nach erfolgreichem Download)
            await DownloadManager.Instance.SaveCompletedDownloadAsync(task, downloadedBytes);
            
            // Fire Completed Event
            DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
            {
                VideoId = task.VideoId,
                VideoType = task.VideoType,
                LocalFilePath = task.LocalFilePath,
                Success = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Download failed: {task.Title} - {ex.Message}");
            
            // Delete incomplete file
            try
            {
                if (File.Exists(task.LocalFilePath))
                {
                    File.Delete(task.LocalFilePath);
                }
            }
            catch { }
            
            // Fire Completed Event (mit Fehler)
            DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs
            {
                VideoId = task.VideoId,
                VideoType = task.VideoType,
                LocalFilePath = task.LocalFilePath,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
        finally
        {
            // Entferne aus Queue-Tracking
            lock (_queueLock)
            {
                _queuedItems.Remove(key);
            }
        }
    }
}

public class DownloadTask
{
    public long VideoId { get; set; }
    public string VideoType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string LocalFilePath { get; set; } = string.Empty;
    public DownloadRetentionType RetentionType { get; set; }
    public DateTime ExpiresAt { get; set; }
    
    // Video-Metadaten
    public string? Plot { get; set; }
    public string? GenreNames { get; set; }
    public string? ReleaseYear { get; set; }
    
    // Episode-Informationen
    public int? EpisodeNumber { get; set; }
    public int? SeasonNumber { get; set; }
    public string? TVShowName { get; set; }
    
    // Bild-URLs (Server)
    public string? PosterImageUrl { get; set; }
    public string? BannerImageUrl { get; set; }
}

public class DownloadProgressEventArgs : EventArgs
{
    public long VideoId { get; set; }
    public string VideoType { get; set; } = string.Empty;
    public double ProgressPercent { get; set; }
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
}

public class DownloadCompletedEventArgs : EventArgs
{
    public long VideoId { get; set; }
    public string VideoType { get; set; } = string.Empty;
    public string LocalFilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
