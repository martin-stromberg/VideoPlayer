using VideoWebPlayer.Data;

/// <summary>
/// Represents a TV show-to-genre relationship.
/// </summary>
public class TVShowGenre
{
    /// <summary>
    /// Gets or sets the TV show identifier.
    /// </summary>
    public long TVShowId { get; set; }
    /// <summary>
    /// Gets or sets the TV show.
    /// </summary>
    public TVShow TVShow { get; set; }

    /// <summary>
    /// Gets or sets the genre identifier.
    /// </summary>
    public long GenreId { get; set; }
    /// <summary>
    /// Gets or sets the genre.
    /// </summary>
    public Genre Genre { get; set; }
}