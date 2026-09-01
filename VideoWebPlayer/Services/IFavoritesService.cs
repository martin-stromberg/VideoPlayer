using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Provides operations for managing favorites for a specific user.
/// </summary>
public interface IFavoritesService
{
    /// <summary>
    /// Returns all favorites for the given user.
    /// </summary>
    Task<DtoFavoriteEntry[]> GetFavoritesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a favorite entry for the given user.
    /// </summary>
    Task AddFavoriteAsync(string userId, FavoriteEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a favorite entry for the given user.
    /// </summary>
    Task RemoveFavoriteAsync(string userId, FavoriteEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a favorite state for the given media entry.
    /// </summary>
    Task<bool> ToggleFavoriteAsync(string userId, DtoMediaEntry entry, CancellationToken cancellationToken = default);
}
