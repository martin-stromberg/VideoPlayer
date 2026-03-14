/// <summary>
/// Represents a recently accessed media entry.
/// </summary>
public class RecentEntry
{
    /// <summary>
    /// Gets or sets the entry identifier.
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// Gets or sets the media source identifier.
    /// </summary>
    public long MediaSourceId { get; set; } // NEU: Quelle
    /// <summary>
    /// Gets or sets the published timestamp.
    /// </summary>
    public DateTime PublishedAt { get; set; }
    /// <summary>
    /// Gets or sets the created timestamp.
    /// </summary>
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Gets or sets the entry type.
    /// </summary>
    public RecentEntryType Type { get; set; } // Movie, MovieCollection, TVShow, TVShowSeason, TVShowEpisode
    /// <summary>
    /// Gets or sets the movie identifier.
    /// </summary>
    public long? MovieId { get; set; }
    /// <summary>
    /// Gets or sets the movie collection identifier.
    /// </summary>
    public long? MovieCollectionId { get; set; }
    /// <summary>
    /// Gets or sets the TV show identifier.
    /// </summary>
    public long? TVShowId { get; set; }
    /// <summary>
    /// Gets or sets the TV show season identifier.
    /// </summary>
    public long? TVShowSeasonId { get; set; }
    /// <summary>
    /// Gets or sets the TV show episode identifier.
    /// </summary>
    public long? TVShowEpisodeId { get; set; }
}
/// <summary>
/// Describes the type of a recent entry.
/// </summary>
public enum RecentEntryType
{
    /// <summary>
    /// Movie entry.
    /// </summary>
    Movie,
    /// <summary>
    /// Movie collection entry.
    /// </summary>
    MovieCollection,
    /// <summary>
    /// TV show entry.
    /// </summary>
    TVShow,
    /// <summary>
    /// TV show season entry.
    /// </summary>
    TVShowSeason,
    /// <summary>
    /// TV show episode entry.
    /// </summary>
    TVShowEpisode
}