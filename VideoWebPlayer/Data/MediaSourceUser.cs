using VideoWebPlayer.Data;

/// <summary>
/// Represents the association between a user and a media source.
/// </summary>
public class MediaSourceUser
{
    /// <summary>
    /// Gets or sets the media source identifier.
    /// </summary>
    public long MediaSourceId { get; set; }
    /// <summary>
    /// Gets or sets the media source.
    /// </summary>
    public MediaSource MediaSource { get; set; }
    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public string UserId { get; set; }
    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    public ApplicationUser User { get; set; }
}