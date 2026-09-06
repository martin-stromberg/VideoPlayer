using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Services;

/// <summary>
/// Provides CRUD operations for managing playlists for a specific user.
/// </summary>
public interface IPlaylistService
{
    /// <summary>
    /// Returns all playlists for the given user.
    /// </summary>
    Task<DtoPlaylist[]> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single playlist for the given user, or <c>null</c> if it does not exist.
    /// </summary>
    Task<DtoPlaylist?> GetPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new playlist for the given user.
    /// </summary>
    Task<DtoPlaylist> CreatePlaylistAsync(string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing playlist for the given user.
    /// </summary>
    Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a playlist for the given user.
    /// </summary>
    Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a media entry (with cascade logic for TVShow, TVShowSeason and MovieCollection) to a playlist.
    /// </summary>
    Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a media entry from a playlist.
    /// </summary>
    Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all entries of a playlist, silently removing orphaned entries whose referenced media no longer exists.
    /// </summary>
    Task<DtoPlaylistEntry[]> GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a sorted, paginated page of entries of a playlist, silently removing orphaned entries whose
    /// referenced media no longer exists.
    /// </summary>
    Task<DtoPlaylistEntriesPagedResult> GetPlaylistEntriesPagedAsync(long playlistId, string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
