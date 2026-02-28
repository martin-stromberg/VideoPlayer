using VideoWebPlayer.Data;

/// <summary>
/// Represents a movie-to-genre relationship.
/// </summary>
public class MovieGenre
{
    /// <summary>
    /// Gets or sets the movie identifier.
    /// </summary>
    public long MovieId { get; set; }
    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    public Movie Movie { get; set; }

    /// <summary>
    /// Gets or sets the genre identifier.
    /// </summary>
    public long GenreId { get; set; }
    /// <summary>
    /// Gets or sets the genre.
    /// </summary>
    public Genre Genre { get; set; }
}