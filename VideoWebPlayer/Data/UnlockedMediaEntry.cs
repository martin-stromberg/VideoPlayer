using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data;

/// <summary>
/// Represents a media entry that an administrator explicitly unlocked for a specific user,
/// even if the source itself is not directly shared with that user.
/// </summary>
public class UnlockedMediaEntry
{
    /// <summary>
    /// Gets or sets the entry identifier.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the user identifier the entry is unlocked for.
    /// </summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user the entry is unlocked for.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Gets or sets the movie collection identifier, if a collection is unlocked.
    /// </summary>
    public long? MovieCollectionId { get; set; }

    /// <summary>
    /// Gets or sets the TV show identifier, if a show is unlocked.
    /// </summary>
    public long? TVShowId { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
