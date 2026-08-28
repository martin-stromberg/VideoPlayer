using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements media unlocking operations backed by <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class UnlockedMediaService : IUnlockedMediaService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthService _authService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnlockedMediaService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="authService">Authentication service.</param>
    public UnlockedMediaService(ApplicationDbContext db, IAuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    /// <inheritdoc />
    public async Task<bool> IsUnlockedAsync(DtoMediaEntry entry, CancellationToken cancellationToken = default)
    {
        var (movieCollectionId, tvShowId) = GetIds(entry);
        if (movieCollectionId == null && tvShowId == null)
            return false;

        var currentUser = _authService.CurrentUser;
        if (currentUser is null)
            return false;

        return await _db.UnlockedMediaEntries
            .AsNoTracking()
            .AnyAsync(u =>
                u.UserId == currentUser.Id &&
                ((movieCollectionId != null && u.MovieCollectionId == movieCollectionId) ||
                (tvShowId != null && u.TVShowId == tvShowId)),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string[]> GetUnlockedUserIdsAsync(DtoMediaEntry entry, CancellationToken cancellationToken = default)
    {
        var (movieCollectionId, tvShowId) = GetIds(entry);
        if (movieCollectionId == null && tvShowId == null)
            return Array.Empty<string>();

        return await _db.UnlockedMediaEntries
            .AsNoTracking()
            .Where(u =>
                (movieCollectionId != null && u.MovieCollectionId == movieCollectionId) ||
                (tvShowId != null && u.TVShowId == tvShowId))
            .Select(u => u.UserId)
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetUnlockedUsersAsync(DtoMediaEntry entry, string[] userIds, CancellationToken cancellationToken = default)
    {
        if (userIds is null)
            throw new ArgumentNullException(nameof(userIds));

        var (movieCollectionId, tvShowId) = GetIds(entry);
        if (movieCollectionId == null && tvShowId == null)
            throw new ArgumentException("Nur Serien oder Filmsammlungen können freigeschaltet werden.", nameof(entry));

        var existing = await _db.UnlockedMediaEntries
            .Where(u =>
                (movieCollectionId != null && u.MovieCollectionId == movieCollectionId) ||
                (tvShowId != null && u.TVShowId == tvShowId))
            .ToListAsync(cancellationToken);

        _db.UnlockedMediaEntries.RemoveRange(existing);

        var validUserIds = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var userId in validUserIds.Distinct())
        {
            _db.UnlockedMediaEntries.Add(new UnlockedMediaEntry
            {
                UserId = userId,
                MovieCollectionId = movieCollectionId,
                TVShowId = tvShowId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long[]> GetUnlockedMovieCollectionIdsForUserAsync(string userId, CancellationToken cancellationToken = default)
        => await _db.UnlockedMediaEntries
            .AsNoTracking()
            .Where(u => u.UserId == userId && u.MovieCollectionId != null)
            .Select(u => u.MovieCollectionId!.Value)
            .ToArrayAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<long[]> GetUnlockedTVShowIdsForUserAsync(string userId, CancellationToken cancellationToken = default)
        => await _db.UnlockedMediaEntries
            .AsNoTracking()
            .Where(u => u.UserId == userId && u.TVShowId != null)
            .Select(u => u.TVShowId!.Value)
            .ToArrayAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<long[]> GetUnlockedSourceIdsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var unlockedCollectionIds = await GetUnlockedMovieCollectionIdsForUserAsync(userId, cancellationToken);
        var unlockedShowIds = await GetUnlockedTVShowIdsForUserAsync(userId, cancellationToken);

        var collectionSources = await _db.MovieCollections
            .AsNoTracking()
            .Where(c => unlockedCollectionIds.Contains(c.Id))
            .Select(c => c.MediaSourceId)
            .ToArrayAsync(cancellationToken);

        var showSources = await _db.TVShows
            .AsNoTracking()
            .Where(s => unlockedShowIds.Contains(s.Id))
            .Select(s => s.MediaSourceId)
            .ToArrayAsync(cancellationToken);

        return collectionSources.Concat(showSources).Distinct().ToArray();
    }

    private static (long? MovieCollectionId, long? TVShowId) GetIds(DtoMediaEntry entry)
        => entry switch
        {
            DtoMovieCollection => (entry.Id, null),
            DtoTVShow => (null, entry.Id),
            _ => (null, null)
        };
}
