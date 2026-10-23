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

    /// <summary>
    /// Gets or sets a value indicating whether the linked media item is a trailer of the movie
    /// (Plex/Jellyfin convention: a <c>-trailer</c>-suffixed file or a file inside a
    /// <c>trailers</c> subfolder) instead of one of its playable main files.
    /// Trailer links are never picked as the movie's representative media item; they feed the
    /// "trailer of upcoming movies" filler material of the automated TV channel.
    /// </summary>
    public bool IsTrailer { get; set; }
}