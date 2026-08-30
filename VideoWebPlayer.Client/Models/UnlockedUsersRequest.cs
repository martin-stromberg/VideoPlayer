namespace VideoWebPlayer.Client.Models;

/// <summary>
/// Request to set the users an individual media entry is unlocked for.
/// </summary>
public sealed class UnlockedUsersRequest
{
    /// <summary>
    /// The media entry to unlock.
    /// </summary>
    public DtoMediaEntry Entry { get; set; } = null!;

    /// <summary>
    /// The ids of the users to unlock the entry for.
    /// </summary>
    public string[] UserIds { get; set; } = [];
}
