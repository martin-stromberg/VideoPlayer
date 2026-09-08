using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Components.Playlists;

/// <summary>
/// Shared "playlist pending deletion" state for the delete confirmation dialog (<c>PlaylistDeleteConfirmationDialog</c>),
/// used by both <c>PlaylistDetail</c> and <c>PlaylistsList</c> instead of each duplicating its own
/// <c>playlistPendingDelete</c> field and <c>RequestDelete</c>/<c>CancelDelete</c> methods, analogous to
/// <see cref="PlaylistFormState"/> for the create/edit form dialog.
/// </summary>
public sealed class PlaylistDeletePendingState
{
    /// <summary>
    /// The playlist currently pending deletion (confirmation dialog open), or <c>null</c> if none.
    /// </summary>
    public DtoPlaylist? Pending { get; private set; }

    /// <summary>
    /// Opens the delete confirmation dialog for the given playlist.
    /// </summary>
    /// <param name="playlist">The playlist to request deletion for.</param>
    public void Request(DtoPlaylist playlist)
    {
        Pending = playlist;
    }

    /// <summary>
    /// Closes the delete confirmation dialog without deleting anything.
    /// </summary>
    public void Cancel()
    {
        Pending = null;
    }
}
