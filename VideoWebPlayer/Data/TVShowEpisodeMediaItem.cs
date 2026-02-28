using VideoWebPlayer.Data;

/// <summary>
/// Represents a relationship between a TV show episode and a media item.
/// </summary>
public class TVShowEpisodeMediaItem
{
    /// <summary>
    /// Gets or sets the TV show episode identifier.
    /// </summary>
    public long TVShowEpisodeId { get; set; }
    /// <summary>
    /// Gets or sets the TV show episode.
    /// </summary>
    public TVShowEpisode TVShowEpisode { get; set; } = null!;

    /// <summary>
    /// Gets or sets the media item identifier.
    /// </summary>
    public long MediaItemId { get; set; }
    /// <summary>
    /// Gets or sets the media item.
    /// </summary>
    public MediaItem MediaItem { get; set; } = null!;
}