namespace VideoWebPlayer.Maui.Models;

public class VideoRequest
{
    public long VideoId { get; set; }
    
    public string VideoType { get; set; } = string.Empty; // "Movie" or "Episode"
    
    public string Title { get; set; } = string.Empty;
    
    public VideoSourceInfo? SourceInfo { get; private set; }
    
    public bool IsReady => SourceInfo != null;
    
    public event EventHandler<VideoSourceInfo>? SourceAvailable;
    
    internal void SetSource(VideoSourceInfo sourceInfo)
    {
        SourceInfo = sourceInfo;
        SourceAvailable?.Invoke(this, sourceInfo);
    }
}

public class VideoSourceInfo
{
    public string SourcePath { get; set; } = string.Empty;
    
    public VideoSourceType SourceType { get; set; }
    
    public TimeSpan? ResumePosition { get; set; }
    
    public TimeSpan? Duration { get; set; }
}

public enum VideoSourceType
{
    LocalFile,
    StreamUrl
}
