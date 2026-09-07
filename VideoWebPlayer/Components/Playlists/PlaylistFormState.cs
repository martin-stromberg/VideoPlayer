namespace VideoWebPlayer.Components.Playlists;

/// <summary>
/// Shared open/close state for the playlist create/edit form dialog (<c>PlaylistForm</c>), used by both
/// <c>PlaylistDetail</c> and <c>PlaylistsList</c> instead of each duplicating its own
/// <c>showForm</c>/<c>editingPlaylistId</c> fields and <c>OpenEditForm</c>/<c>HandleCancel</c> methods.
/// </summary>
public sealed class PlaylistFormState
{
    /// <summary>
    /// The id of the playlist currently being edited, or <c>null</c> when creating a new playlist.
    /// </summary>
    public long? EditingId { get; private set; }

    /// <summary>
    /// Whether the form dialog is currently open.
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// Opens the form dialog for creating a new playlist.
    /// </summary>
    public void OpenForCreate()
    {
        EditingId = null;
        IsOpen = true;
    }

    /// <summary>
    /// Opens the form dialog for editing the playlist with the given id.
    /// </summary>
    /// <param name="playlistId">The id of the playlist to edit.</param>
    public void OpenForEdit(long playlistId)
    {
        EditingId = playlistId;
        IsOpen = true;
    }

    /// <summary>
    /// Closes the form dialog and resets <see cref="EditingId"/>, so a subsequent <see cref="OpenForCreate"/>
    /// (or an accidental re-check of <see cref="EditingId"/> while the dialog is closed) never sees a
    /// stale id left over from the previous edit.
    /// </summary>
    public void Close()
    {
        EditingId = null;
        IsOpen = false;
    }
}
