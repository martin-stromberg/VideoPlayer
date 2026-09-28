namespace VideoWebPlayer.ViewModels
{
    /// <summary>
    /// Immutable, client-side-only representation of the playlist a video is currently being played
    /// from: which playlist, which entry, how many entries in total, and the playlist's name. Never
    /// persisted server-side - reconstructed by <c>VideoPlayer.razor</c> from the component parameters
    /// supplied by whichever page started the playback (e.g. <c>PlaylistDetail.razor</c>).
    /// </summary>
    /// <param name="PlaylistId">The id of the playlist being played.</param>
    /// <param name="CurrentEntryId">The id of the playlist entry currently playing.</param>
    /// <param name="TotalCount">The total number of entries in the playlist.</param>
    /// <param name="PlaylistName">The name of the playlist, for badge display.</param>
    /// <returns>A new <see cref="PlaylistPlaybackContext"/> instance.</returns>
    public sealed record PlaylistPlaybackContext(long PlaylistId, long CurrentEntryId, int TotalCount, string PlaylistName);
}
