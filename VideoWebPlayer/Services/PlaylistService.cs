using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Events;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements playlist CRUD operations backed by <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class PlaylistService : IPlaylistService
{
    private readonly ApplicationDbContext _db;
    private readonly EventManager _eventManager;
    private readonly PlaylistSettings _playlistSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="eventManager">Event manager used for publishing playlist events.</param>
    /// <param name="playlistSettings">Optional playlist configuration.</param>
    public PlaylistService(
        ApplicationDbContext db,
        EventManager eventManager,
        IOptions<PlaylistSettings>? playlistSettings = null)
    {
        _db = db;
        _eventManager = eventManager;
        _playlistSettings = playlistSettings?.Value ?? new PlaylistSettings();
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist[]> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var playlists = await _db.Playlists
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        return playlists.Select(ToDto).ToArray();
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist?> GetPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);
        if (playlist is null)
            return null;

        if (playlist.UserId != userId)
            throw new PlaylistAccessDeniedException("Sie haben keinen Zugriff auf diese Playlist.");

        return ToDto(playlist);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist> CreatePlaylistAsync(string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)
    {
        var trimmedName = ValidateName(name);
        var trimmedDescription = ValidateDescription(description);
        var resolvedSortMode = ParseSortMode(sortMode, PlaylistSortMode.ByReleaseDate);

        var isDuplicate = await _db.Playlists
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
        if (isDuplicate)
            throw new InvalidOperationException("Ein Playlist mit diesem Namen existiert bereits.");

        if (_playlistSettings.MaxPlaylistsPerUser is int maxPlaylists)
        {
            var currentCount = await _db.Playlists.AsNoTracking().CountAsync(p => p.UserId == userId, cancellationToken);
            if (currentCount >= maxPlaylists)
                throw new InvalidOperationException("Die maximale Anzahl an Playlists wurde erreicht.");
        }

        var now = DateTime.UtcNow;
        var playlist = new Playlist
        {
            UserId = userId,
            Name = trimmedName,
            Description = trimmedDescription,
            SortMode = resolvedSortMode,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.Playlists.AddAsync(playlist, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _eventManager.Publish(new PlaylistCreatedEvent(playlist));

        return ToDto(playlist);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken)
            ?? throw new KeyNotFoundException("Playlist wurde nicht gefunden.");

        if (playlist.UserId != userId)
            throw new PlaylistAccessDeniedException("Sie haben keinen Zugriff auf diese Playlist.");

        var trimmedName = ValidateName(name);
        var trimmedDescription = ValidateDescription(description);
        var resolvedSortMode = ParseSortMode(sortMode, playlist.SortMode);

        var isDuplicate = await _db.Playlists
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.Id != playlistId && p.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
        if (isDuplicate)
            throw new InvalidOperationException("Ein Playlist mit diesem Namen existiert bereits.");

        playlist.Name = trimmedName;
        playlist.Description = trimmedDescription;
        playlist.SortMode = resolvedSortMode;
        playlist.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        _eventManager.Publish(new PlaylistUpdatedEvent(playlist));

        return ToDto(playlist);
    }

    /// <inheritdoc />
    public async Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken)
            ?? throw new KeyNotFoundException("Playlist wurde nicht gefunden.");

        if (playlist.UserId != userId)
            throw new PlaylistAccessDeniedException("Sie haben keinen Zugriff auf diese Playlist.");

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync(cancellationToken);
        _eventManager.Publish(new PlaylistDeletedEvent(playlistId, userId));
    }

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Playlist-Name ist erforderlich.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > 255)
            throw new InvalidOperationException("Name darf maximal 255 Zeichen lang sein.");

        return trimmedName;
    }

    private static string? ValidateDescription(string? description)
    {
        if (string.IsNullOrEmpty(description))
            return null;

        if (description.Length > 2000)
            throw new InvalidOperationException("Beschreibung darf maximal 2000 Zeichen lang sein.");

        return description;
    }

    private static PlaylistSortMode ParseSortMode(string? sortMode, PlaylistSortMode fallback)
    {
        if (string.IsNullOrWhiteSpace(sortMode))
            return fallback;

        if (!Enum.TryParse<PlaylistSortMode>(sortMode, ignoreCase: true, out var parsed))
            throw new InvalidOperationException("Ungueltiger Sortiermodus.");

        return parsed;
    }

    private static DtoPlaylist ToDto(Playlist playlist) => new()
    {
        Id = playlist.Id,
        Name = playlist.Name,
        Description = playlist.Description,
        SortMode = playlist.SortMode.ToString(),
        CreatedAt = playlist.CreatedAt,
        UpdatedAt = playlist.UpdatedAt
    };
}
