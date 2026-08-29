using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Services;

/// <summary>
/// Provides operations for explicitly unlocking individual media entries (TV shows, movie collections)
/// for specific users, independent of the source-level sharing.
/// </summary>
public interface IUnlockedMediaService
{
    /// <summary>
    /// Gets the unlocked state of the given media entry for the current user.
    /// </summary>
    Task<bool> IsUnlockedAsync(DtoMediaEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ids of all users the given media entry is unlocked for.
    /// </summary>
    Task<string[]> GetUnlockedUserIdsAsync(DtoMediaEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the users the given media entry is unlocked for (replaces existing entries).
    /// </summary>
    Task SetUnlockedUsersAsync(DtoMediaEntry entry, string[] userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the ids of all movie collections that are explicitly unlocked for the given user.
    /// </summary>
    Task<long[]> GetUnlockedMovieCollectionIdsForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the ids of all TV shows that are explicitly unlocked for the given user.
    /// </summary>
    Task<long[]> GetUnlockedTVShowIdsForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the media source ids that contain at least one entry unlocked for the given user.
    /// </summary>
    Task<long[]> GetUnlockedSourceIdsForUserAsync(string userId, CancellationToken cancellationToken = default);
}
