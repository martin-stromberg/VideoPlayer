using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data;

/// <summary>
/// Stores the watched timestamp for a concrete movie or episode per user.
/// </summary>
public sealed class WatchedEntry
{
    /// <summary>
    /// Gets or sets the entry identifier.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the owning user identifier.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the watched movie identifier.
    /// </summary>
    public long? MovieId { get; set; }

    /// <summary>
    /// Gets or sets the watched episode identifier.
    /// </summary>
    public long? TVShowEpisodeId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the title was watched.
    /// </summary>
    public DateTime WatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the owning user.
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the watched movie.
    /// </summary>
    [ForeignKey(nameof(MovieId))]
    public Movie? Movie { get; set; }

    /// <summary>
    /// Gets or sets the watched episode.
    /// </summary>
    [ForeignKey(nameof(TVShowEpisodeId))]
    public TVShowEpisode? TVShowEpisode { get; set; }
}
