using VideoWebPlayer.Data;

/// <summary>
/// Represents a relationship between a movie and a media item.
/// </summary>
public class MovieMediaItem
{
    /// <summary>
    /// Gets or sets the movie identifier.
    /// </summary>
    public long MovieId { get; set; }
    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    public Movie Movie { get; set; } = null!;

    /// <summary>
    /// Gets or sets the media item identifier.
    /// </summary>
    public long MediaItemId { get; set; }
    /// <summary>
    /// Gets or sets the media item.
    /// </summary>
    public MediaItem MediaItem { get; set; } = null!;
}