using SQLite;

namespace VideoWebPlayer.Maui.Models;

[Table("Downloads")]
public class DownloadedVideo
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public long VideoId { get; set; }
    
    [Indexed]
    public string VideoType { get; set; } = string.Empty; // "Movie" or "Episode"
    
    public string LocalFilePath { get; set; } = string.Empty;
    
    public long FileSizeBytes { get; set; }
    
    public DownloadRetentionType RetentionType { get; set; }
    
    [Indexed]
    public DateTime DownloadedAt { get; set; }
    
    [Indexed]
    public DateTime ExpiresAt { get; set; }
    
    public string Title { get; set; } = string.Empty;
    
    // Video-Informationen
    public string? Plot { get; set; }
    public string? GenreNames { get; set; }
    public string? ReleaseYear { get; set; }
    
    // Episode-spezifische Informationen
    public int? EpisodeNumber { get; set; }
    public int? SeasonNumber { get; set; }
    public string? TVShowName { get; set; }
    
    // Lokale Bild-Pfade
    public string? LocalPosterImagePath { get; set; }
    public string? LocalBannerImagePath { get; set; }
    
    public long? MovieId { get; set; }
    
    public long? EpisodeId { get; set; }
    
    public long? SeasonId { get; set; }
    
    public long? TVShowId { get; set; }
    
    public DownloadStatus Status { get; set; }
    
    public double ProgressPercent { get; set; }
    
    // Abspielposition in Sekunden
    public double PlaybackPositionSeconds { get; set; }
    
    // Gesamtdauer in Sekunden
    public double DurationSeconds { get; set; }
}

public enum DownloadRetentionType
{
    Cache = 0,      // 1 Tag
    Download = 1    // 7 Tage
}

public enum DownloadStatus
{
    Queued,
    Downloading,
    Completed,
    Failed
}
